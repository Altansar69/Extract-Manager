using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace ExtractManager;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private static readonly JsonSerializerOptions SJsonOptions = new() { WriteIndented = true };
    private readonly string _passwordsFilePath;
    private readonly string _settingsFilePath;
    private ArchiveExtractor? _extractor;
    private string? _latest7ZDownloadUrl;

    public MainWindow()
    {
        InitializeComponent();

        RemovePasswordCommand = new RelayCommand(param =>
        {
            if (param is string password) Passwords.Remove(password);
        });

        _passwordsFilePath = Path.Combine(AppContext.BaseDirectory, "passwords.json");
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        LoadSettings();
        // ApplyWindowSettings is now called in the Loaded event
        LoadPasswords();
        Passwords.CollectionChanged += Passwords_CollectionChanged;
        AppLogger.LogReceived += OnLogReceived;

        // Use informational version (build-time generated) instead of AssemblyVersion
        var infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        AppVersion = infoVersion is { Length: > 0 } ? $"Version {infoVersion}" : "Version 1.0.0";

        DataContext = this;

        var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
        if (File.Exists(sevenZipPath)) _extractor = new ArchiveExtractor(sevenZipPath);

        Loaded += async (_, _) =>
        {
            ApplyWindowSettings(); // Apply settings after the window is fully loaded
            try
            {
                await UpdateLocal7ZVersionDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to display 7-Zip version: {ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    public ICommand RemovePasswordCommand { get; }
    public string AppVersion { get; }
    public ObservableCollection<string> Passwords { get; set; } = [];
    public AppSettings Settings { get; set; } = new();

    private void OnLogReceived(string message)
    {
        Dispatcher.Invoke(() =>
        {
            LogParagraph.Inlines.Add(new Run(message + Environment.NewLine));
            LogRichTextBox.ScrollToEnd();
        });
    }

    [GeneratedRegex(@"(\d+\.\d+).*: (\d{4}-\d{2}-\d{2})")]
    private static partial Regex SevenZipCliVersionRegex();

    [GeneratedRegex(@"(\d+\.\d+)")]
    private static partial Regex SevenZipVersionOnlyRegex();

    private static async Task BlinkButtonAsync(Button button)
    {
        var originalBrush = button.Background;
        var successBrush = Application.Current.FindResource("SuccessBrush") as Brush ??
                           new SolidColorBrush(Colors.Green);

        static Color ResolveColor(Brush b, Color fallback)
        {
            return b is SolidColorBrush scb ? scb.Color : fallback;
        }

        var fromColor = ResolveColor(originalBrush, Colors.Transparent);
        var toColor = ResolveColor(successBrush, Colors.Green);

        var animation = new ColorAnimation
        {
            To = toColor,
            From = fromColor,
            AutoReverse = true,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            RepeatBehavior = new RepeatBehavior(2)
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, button);
        Storyboard.SetTargetProperty(animation, new PropertyPath("Background.Color"));

        // Ensure the Background is a SolidColorBrush to animate its Color property
        button.Background = new SolidColorBrush(fromColor);

        storyboard.Begin();
        await Task.Delay(1000);
    }


    public void OpenExtractionWindowFor(string archivePath)
    {
        if (_extractor == null)
        {
            MessageBox.Show(
                "7za.exe not found. Please download it via the Settings tab before extracting.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var extractionWindow = new ExtractionWindow(archivePath, [.. Passwords], _extractor);
        extractionWindow.ShowDialog();
    }

    private void AddPassword_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordTextBox.Text;
        if (string.IsNullOrWhiteSpace(password)) return;

        if (TryAddPassword(password)) PasswordTextBox.Clear();
    }

    private bool TryAddPassword(string password)
    {
        if (Passwords.Contains(password)) return false; // Exact match already exists

        var existingPassword =
            Passwords.FirstOrDefault(p => p.Equals(password, StringComparison.OrdinalIgnoreCase));

        if (existingPassword != null)
        {
            var result = MessageBox.Show(
                $"The password '{existingPassword}' already exists. Passwords are case-sensitive.\n\n" +
                $"Do you want to add '{password}' as a new, separate password?",
                "Case-Sensitive Password Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No) return false;
        }

        Passwords.Add(password);
        return true;
    }

    private void RemovePassword_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordListBox.SelectedItem is string selectedPassword) Passwords.Remove(selectedPassword);
    }


    private void LoadPasswords()
    {
        if (File.Exists(_passwordsFilePath))
        {
            var json = File.ReadAllText(_passwordsFilePath);
            var passwords = JsonSerializer.Deserialize<List<string>>(json);
            Passwords = new ObservableCollection<string>(passwords ?? []);
        }
        else
        {
            Passwords = [];
        }
    }

    private void SavePasswords()
    {
        var json = JsonSerializer.Serialize(Passwords, SJsonOptions);
        File.WriteAllText(_passwordsFilePath, json);
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            var json = File.ReadAllText(_settingsFilePath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        else
        {
            Settings = new AppSettings();
        }

        AppLogger.Settings = Settings;
    }

    private void ApplyWindowSettings()
    {
        if (!Settings.SaveWindowBounds) return;

        var savedBounds = new Rect(Settings.WindowLeft, Settings.WindowTop, Settings.WindowWidth,
            Settings.WindowHeight);
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

        if (virtualScreen.IntersectsWith(savedBounds))
        {
            Top = Settings.WindowTop;
            Left = Settings.WindowLeft;
            Width = Settings.WindowWidth;
            Height = Settings.WindowHeight;

            // Restore the window state AFTER setting position and size
            WindowState = Settings.WindowState;
        }
    }

    private void SaveSettings()
    {
        if (Settings.SaveWindowBounds)
        {
            // Always save the WindowState
            Settings.WindowState = WindowState;

            // Use RestoreBounds if the window is maximized, otherwise use current dimensions
            if (WindowState == WindowState.Maximized)
            {
                Settings.WindowTop = RestoreBounds.Top;
                Settings.WindowLeft = RestoreBounds.Left;
                Settings.WindowHeight = RestoreBounds.Height;
                Settings.WindowWidth = RestoreBounds.Width;
            }
            else
            {
                Settings.WindowTop = Top;
                Settings.WindowLeft = Left;
                Settings.WindowHeight = Height;
                Settings.WindowWidth = Width;
            }
        }

        var json = JsonSerializer.Serialize(Settings, SJsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSettings();
        SavePasswords();
        AppLogger.LogReceived -= OnLogReceived;
    }

    private void Passwords_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SavePasswords();
    }

    private void ExtractArchive_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select an Archive",
            Filter = "Archive Files|*.7z;*.zip;*.rar;*.tar;*.gz;*.bz2;*.xz;*.wim;*.iso|All Files|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            var archivePath = openFileDialog.FileName;
            OpenExtractionWindowFor(archivePath);
        }
    }

    private void PasswordTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) AddPassword_Click(sender, e);
    }

    private void VersionText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        const string infoUrl = "https://cs.rin.ru/forum/viewtopic.php?p=3356700#p3356700";
        try
        {
            Process.Start(new ProcessStartInfo(infoUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the link: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            AppLogger.Log($"Failed to open URL '{infoUrl}': {ex.Message}");
        }
    }

    private async void ExportPasswords_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var json = JsonSerializer.Serialize(Passwords.ToList());
            var encrypted = Crypto.Encrypt(json);
            ImportExportTextBox.Text = encrypted;
            Clipboard.SetText(encrypted);
            if (sender is Button button) await BlinkButtonAsync(button);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export passwords: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void ImportPasswords_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var encrypted = ImportExportTextBox.Text;
            if (string.IsNullOrWhiteSpace(encrypted))
            {
                MessageBox.Show("Import box is empty. Paste an exported password string to import.", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var json = Crypto.Decrypt(encrypted);
            if (json == null)
            {
                MessageBox.Show("Invalid format. The provided string could not be decrypted.", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var importedPasswords = JsonSerializer.Deserialize<List<string>>(json);
            if (importedPasswords == null)
            {
                MessageBox.Show("The data is corrupt and could not be read as a password list.", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                "Do you want to merge with the existing password list? \n\n" +
                "• Yes: Merge the new passwords, avoiding duplicates.\n" +
                "• No: Replace the current list with the new one.\n" +
                "• Cancel: Do nothing.",
                "Confirm Import Action",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes: // Merge
                    foreach (var pass in importedPasswords) TryAddPassword(pass);
                    break;
                case MessageBoxResult.No: // Overwrite
                    Passwords.Clear();
                    foreach (var pass in importedPasswords) TryAddPassword(pass);
                    break;
                case MessageBoxResult.Cancel: // Cancel
                    return;
            }


            if (sender is Button button) await BlinkButtonAsync(button);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to import passwords: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void RegisterContextMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellContextMenu.Register();
            if (sender is Button button) await BlinkButtonAsync(button);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Administrator privileges are required to register the context menu. Please restart the application as an administrator.",
                "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UnregisterContextMenu_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellContextMenu.Unregister();
            if (sender is Button button) await BlinkButtonAsync(button);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Administrator privileges are required to unregister the context menu. Please restart the application as an administrator.",
                "Permission Denied", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdateLocal7ZVersionDisplay()
    {
        if (_extractor == null)
        {
            SevenZipVersionTextBlock.Text = "Current version: Not found. Please download.";
            Update7ZButton.IsEnabled = true; // Allow download if not found
            return;
        }

        var output = await _extractor.GetVersionOutputAsync();
        var match = SevenZipCliVersionRegex().Match(output);
        SevenZipVersionTextBlock.Text = match.Success
            ? $"Current version: {match.Groups[1].Value} (Released: {match.Groups[2].Value})"
            : "Current version: Unknown";
    }

    private async Task<bool> CheckForUpdateLogicAsync()
    {
        var localVersionOutput = _extractor != null ? await _extractor.GetVersionOutputAsync() : "0.0";
        var localVersionMatch = SevenZipVersionOnlyRegex().Match(localVersionOutput);
        var localVersion = localVersionMatch.Success ? localVersionMatch.Groups[1].Value : "0.0";

        var (downloadUrl, latestVersion) = await SevenZipManager.GetLatestReleaseInfoAsync();

        bool updateAvailable;
        if (Version.TryParse(localVersion, out var lv) && Version.TryParse(latestVersion, out var rv))
            updateAvailable = lv < rv;
        else
            updateAvailable = localVersion != latestVersion;

        if (updateAvailable)
        {
            _latest7ZDownloadUrl = downloadUrl;
            MessageBox.Show($"An update is available! Version {latestVersion} can be downloaded.",
                "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
            Update7ZButton.IsEnabled = true;
        }
        else
        {
            _latest7ZDownloadUrl = null;
            MessageBox.Show("You already have the latest version of 7-Zip.", "Up to Date", MessageBoxButton.OK,
                MessageBoxImage.Information);
            Update7ZButton.IsEnabled = false;
        }

        return updateAvailable;
    }

    private async void CheckFor7zUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await CheckForUpdateLogicAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void Update7z_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_latest7ZDownloadUrl))
            {
                var updateAvailable = await CheckForUpdateLogicAsync();
                if (!updateAvailable || string.IsNullOrEmpty(_latest7ZDownloadUrl)) return;
            }

            Update7ZButton.IsEnabled = false;
            await SevenZipManager.DownloadAndExtractAsync(_latest7ZDownloadUrl);
            if (sender is Button button) await BlinkButtonAsync(button);

            var sevenZipPath = Path.Combine(AppContext.BaseDirectory, "7za.exe");
            _extractor = new ArchiveExtractor(sevenZipPath);

            await UpdateLocal7ZVersionDisplay();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Update7ZButton.IsEnabled = true;
        }
    }
}