using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace ExtractManager;

public partial class ExtractionWindow
{
    private readonly string _archivePath;
    private readonly ArchiveExtractor _extractor;
    private readonly List<string> _passwords;

    public ExtractionWindow(string archivePath, List<string> passwords, ArchiveExtractor extractor)
    {
        InitializeComponent();

        if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            Owner = Application.Current.MainWindow;

        _archivePath = archivePath;
        _passwords = passwords;
        _extractor = extractor;

        ArchiveFileTextBox.Text = _archivePath;
        OutputPathTextBox.Text = Path.GetDirectoryName(_archivePath) ?? string.Empty;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartButton.IsEnabled = false;
            await PerformExtraction();
        }
        catch (Exception ex)
        {
            Log($"A critical error occurred: {ex.Message}", false);
            CloseButton.IsEnabled = true;
        }
    }

    private async Task PerformExtraction()
    {
        AppLogger.Log("PerformExtraction started.");
        Log("Starting extraction process...");
        var outputPath = OutputPathTextBox.Text;

        if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
        {
            Log("Invalid output directory specified.", false);
            StartButton.IsEnabled = true;
            CloseButton.IsEnabled = true;
            return;
        }

        try
        {
            var isProtected = await Task.Run(() => _extractor.IsPasswordProtected(_archivePath));

            if (isProtected)
            {
                if (_passwords.Count <= 0)
                {
                    Log("Extraction failed. The archive is password-protected, but your password list is empty.",
                        false);
                    return;
                }

                Log("Archive is password protected. Attempting to extract with saved passwords...");
                var result = await Task.Run(() =>
                    _extractor.ExtractWithPasswordList(_archivePath, outputPath, _passwords));
                if (result.Success)
                    Log($"Extraction successful! Password: {result.Password}", true);
                else
                    Log($"Extraction failed. {result.ErrorMessage}", false);
            }
            else
            {
                Log("Archive is not password protected. Extracting...");
                var result = await Task.Run(() => _extractor.ExtractWithoutPassword(_archivePath, outputPath));
                if (result.Success)
                    Log("Extraction successful!", true);
                else
                    Log($"Extraction failed. {result.ErrorMessage}", false);
            }
        }
        catch (Exception ex)
        {
            Log($"An unexpected error occurred: {ex.Message}", false);
        }
        finally
        {
            Log("Process complete.");
            CloseButton.IsEnabled = true;
        }
    }

    private void Log(string message, bool? isSuccess = null)
    {
        var status = isSuccess switch
        {
            true => "[+] ",
            false => "[-] ",
            _ => "[*] "
        };

        var color = isSuccess switch
        {
            true => Application.Current.FindResource("SuccessBrush") as Brush ?? Brushes.Green,
            false => Application.Current.FindResource("FailureBrush") as Brush ?? Brushes.Red,
            _ => Application.Current.FindResource("TextBrush") as Brush ?? Brushes.Black
        };

        var run = new Run($"{status}{message}\n") { Foreground = color };
        LogParagraph.Inlines.Add(run);
        LogRichTextBox.ScrollToEnd();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Folder"
        };

        if (dialog.ShowDialog() == true) OutputPathTextBox.Text = dialog.FolderName;
    }
}