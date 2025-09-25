using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ExtractManager;

public static partial class ShellContextMenu
{
    private const string MenuName = "ExtractWithExtractManager";
    private const string MenuText = "Extract with Extract Manager";

    private const long SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;
    private static readonly string[] SFileTypes = [".zip", ".rar", ".7z", ".tar", ".gz"];

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void Register()
    {
        if (!IsAdministrator())
            throw new UnauthorizedAccessException(
                "Administrator privileges are required to register the context menu.");

        var appPath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(appPath)) throw new InvalidOperationException("Could not determine application path.");

        foreach (var fileType in SFileTypes)
        {
            var fileClass = GetFileClass(fileType);
            if (string.IsNullOrEmpty(fileClass)) continue;

            var keyPath = @$"{fileClass}\shell\{MenuName}";
            using var key = Registry.ClassesRoot.CreateSubKey(keyPath);

            key.SetValue(null, MenuText);
            // Set the Icon value to the application's executable path.
            key.SetValue("Icon", $"\"{appPath}\"");

            using var commandKey = key.CreateSubKey("command");
            commandKey.SetValue(null, $"\"{appPath}\" \"%1\"");
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Unregister()
    {
        if (!IsAdministrator())
            throw new UnauthorizedAccessException(
                "Administrator privileges are required to unregister the context menu.");

        foreach (var fileType in SFileTypes)
        {
            var fileClass = GetFileClass(fileType);
            if (string.IsNullOrEmpty(fileClass)) continue;

            var keyPath = @$"{fileClass}\shell\{MenuName}";
            Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false); // Set to false; to not throw on missing key
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static string? GetFileClass(string fileType)
    {
        // Read the default value of HKEY_CLASSES_ROOT\.zip to get the file class (e.g., "WinRAR.ZIP")
        using var key = Registry.ClassesRoot.OpenSubKey(fileType);
        return key?.GetValue(null)?.ToString();
    }

    [LibraryImport("Shell32.dll", EntryPoint = "SHChangeNotify", SetLastError = true)]
    private static partial void SHChangeNotify(long wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}