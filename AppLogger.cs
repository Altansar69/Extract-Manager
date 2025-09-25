using System.IO;

namespace ExtractManager;

public static class AppLogger
{
    private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "debug.log");
    public static AppSettings? Settings { get; set; }

    public static event Action<string>? LogReceived;

    public static void Log(string message)
    {
        var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        // Raise the event for the UI to capture
        LogReceived?.Invoke(logMessage);

        // Write to file if debug logging is enabled
        if (Settings is { IsDebugLoggingEnabled: true })
            try
            {
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception)
            {
                // Ignore exceptions during logging to prevent crashes
            }
    }
}