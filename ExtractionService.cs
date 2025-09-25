using System.IO;

namespace ExtractManager;

public static class ExtractionService
{
    public static async Task PerformSilentExtractionAsync(string archivePath, IReadOnlyList<string> passwords, ArchiveExtractor extractor)
    {
        AppLogger.Log($"Starting silent extraction for: {archivePath}");
        var outputPath = Path.GetDirectoryName(archivePath) ?? AppContext.BaseDirectory;

        try
        {
            var isProtected = await Task.Run(() => extractor.IsPasswordProtected(archivePath));
            if (isProtected)
            {
                if (passwords.Count == 0)
                {
                    AppLogger.Log(
                        "Silent extraction failed: Archive is password-protected, but the password list is empty.");
                    return;
                }

                AppLogger.Log("Archive is password-protected. Attempting extraction.");
                var result = await Task.Run(() =>
                    extractor.ExtractWithPasswordList(archivePath, outputPath, [.. passwords]));
                AppLogger.Log(result.Success
                    ? $"Silent extraction successful! Password: {result.Password}"
                    : $"Silent extraction failed: {result.ErrorMessage}");
            }
            else
            {
                AppLogger.Log("Archive is not password-protected. Attempting extraction.");
                var result = await Task.Run(() => extractor.ExtractWithoutPassword(archivePath, outputPath));
                AppLogger.Log(result.Success
                    ? "Silent extraction successful!"
                    : $"Silent extraction failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"An unexpected error occurred during silent extraction: {ex.Message}");
        }
        finally
        {
            AppLogger.Log("Silent extraction process complete.");
        }
    }
}
