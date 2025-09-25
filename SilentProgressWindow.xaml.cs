using System.Windows;

namespace ExtractManager;

public partial class SilentProgressWindow : Window
{
    public SilentProgressWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }
}
