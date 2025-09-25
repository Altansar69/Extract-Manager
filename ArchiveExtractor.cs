using System.Diagnostics;
using System.Text;

namespace ExtractManager;

public class ExtractionResult
{
    public bool Success { get; set; }
    public string? Password { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ArchiveExtractor(string sevenZipPath)
{
    private static void RunProcess(ProcessStartInfo startInfo, out string output, out string error, out int exitCode)
    {
        AppLogger.Log($"Executing command: {startInfo.FileName} {startInfo.Arguments}");
        using var process = new Process();

        // Ensure no interactive prompts can block: redirect all streams
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        process.StartInfo = startInfo;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data != null) outputBuilder.AppendLine(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data != null) errorBuilder.AppendLine(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        // Close stdin to signal EOF if the process tries to prompt
        try
        {
            process.StandardInput.Close();
        }
        catch
        {
            /* ignore */
        }

        process.WaitForExit();

        output = outputBuilder.ToString().Trim();
        error = errorBuilder.ToString().Trim();
        exitCode = process.ExitCode;
        AppLogger.Log($"Process exited with code: {process.ExitCode}.");
        if (!string.IsNullOrEmpty(error)) AppLogger.Log($"Error output: {error}");
    }

    public async Task<string> GetVersionOutputAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    public bool IsPasswordProtected(string archivePath)
    {
        // Prefer a quick test with empty password to detect protection (handles encrypted headers)
        var testInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            Arguments = $"t -y -p\"\" \"{archivePath}\""
        };
        RunProcess(testInfo, out var tOut, out var tErr, out var tCode);

        if (tErr.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) ||
            tErr.Contains("Can not open encrypted archive", StringComparison.OrdinalIgnoreCase) ||
            tErr.Contains("Headers Error", StringComparison.OrdinalIgnoreCase))
            return true;
        // Some versions print wrong password to stdout
        if (tOut.Contains("Wrong password", StringComparison.OrdinalIgnoreCase)) return true;
        // Non-zero exit with no clear message can also indicate protection
        if (tCode != 0 && string.IsNullOrWhiteSpace(tOut) && string.IsNullOrWhiteSpace(tErr)) return true;

        // Fallback to listing check for cases where files are not encrypted but some content is
        var listInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            Arguments = $"l -slt -y -p\"\" \"{archivePath}\""
        };
        RunProcess(listInfo, out var lOut, out _, out _);
        if (lOut.Contains("Encrypted = +", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public ExtractionResult ExtractWithoutPassword(string archivePath, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            // Use empty password to avoid prompt
            Arguments = $"x \"{archivePath}\" -o\"{outputPath}\" -p\"\" -y"
        };
        RunProcess(startInfo, out var stdout, out var stderr, out var exitCode);

        var wrongPass = stderr.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) ||
                        stdout.Contains("Wrong password", StringComparison.OrdinalIgnoreCase);
        var success = exitCode == 0 && !wrongPass;

        return new ExtractionResult
        {
            Success = success,
            ErrorMessage = success ? string.Empty :
                wrongPass ? "Archive requires a password." :
                string.IsNullOrWhiteSpace(stderr) ? stdout : stderr
        };
    }

    public ExtractionResult ExtractWithPasswordList(string archivePath, string outputPath, List<string> passwords)
    {
        foreach (var raw in passwords)
        {
            var trimmed = raw.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Escape double quotes in password to keep argument quoting intact
            var password = trimmed.Replace("\"", "\\\"");

            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"x \"{archivePath}\" -o\"{outputPath}\" -p\"{password}\" -y"
            };

            RunProcess(startInfo, out var stdout, out var stderr, out var exitCode);

            var wrongPassword = stderr.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) ||
                                stdout.Contains("Wrong password", StringComparison.OrdinalIgnoreCase);
            var ok = exitCode == 0 && !wrongPassword;

            if (ok)
                return new ExtractionResult { Success = true, Password = trimmed };

            if (!wrongPassword)
                return new ExtractionResult
                    { Success = false, ErrorMessage = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr };

            // else: try next password
        }

        return new ExtractionResult { Success = false, ErrorMessage = "Exhausted all passwords. None were correct." };
    }
}