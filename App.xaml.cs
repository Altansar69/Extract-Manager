using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace ExtractManager;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App
{
    // ReSharper disable once AsyncVoidEventHandlerMethod
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        try
        {
            // Initialize settings for the logger first.
            var settings = LoadAppSettings();
            AppLogger.Settings = settings;

            // Always ensure 7za.exe is present on startup.
            await SevenZipManager.EnsureSevenZipIsAvailableAsync();

            // Handle silent extraction if command-line arguments are present.
            if (e.Args.Length > 0 && File.Exists(e.Args[0]))
            {
                // Show transient progress window
                var progress = new SilentProgressWindow($"Extracting '{Path.GetFileName(e.Args[0])}'...");
                progress.Show();

                await HandleSilentExtraction(e.Args[0]);

                progress.Close();
                Shutdown();
                return;
            }

            // Proceed with normal UI launch.
            var mainWindow = new MainWindow();
            mainWindow.Show();
            AppLogger.Log("Application started successfully.");
        }
        catch (Exception ex)
        {
            AppLogger.Log($"A critical unhandled exception occurred on startup: {ex}");
            MessageBox.Show($"A critical error occurred on startup: {ex.Message}", "Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static async Task HandleSilentExtraction(string filePath)
    {
        AppLogger.Log($"Silent extraction mode initiated for: {filePath}");

        // OnStartup already ensured availability; just verify presence and proceed
        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        if (!File.Exists(sevenZipPath))
        {
            AppLogger.Log("Silent extraction failed: 7za.exe is not available.");
            return;
        }

        // Prepare extractor and passwords without creating MainWindow/UI
        var extractor = new ArchiveExtractor(sevenZipPath);
        var passwords = LoadPasswords();

        try
        {
            await ExtractionService.PerformSilentExtractionAsync(filePath, passwords, extractor);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"An unexpected error occurred during silent extraction: {ex.Message}");
        }
    }

    private static List<string> LoadPasswords()
    {
        var passwordsFilePath = Path.Combine(AppContext.BaseDirectory, "passwords.json");
        if (!File.Exists(passwordsFilePath)) return [];

        var json = File.ReadAllText(passwordsFilePath);
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private static AppSettings LoadAppSettings()
    {
        var settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(settingsFilePath)) return new AppSettings();

        var json = File.ReadAllText(settingsFilePath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var exception = e.Exception;
        var errorMessage = $"An unhandled UI exception occurred: {exception.Message}";

        AppLogger.Log(errorMessage + "\n" + exception.StackTrace);

        MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }
}