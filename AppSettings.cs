using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ExtractManager;

public class AppSettings : INotifyPropertyChanged
{
    private bool _isDebugLoggingEnabled;
    private bool _saveWindowBounds;
    private double _windowHeight = 600; // Default value
    private double _windowLeft = 100; // Default value
    private WindowState _windowState = WindowState.Normal;
    private double _windowTop = 100; // Default value
    private double _windowWidth = 800; // Default value

    public bool IsDebugLoggingEnabled
    {
        get => _isDebugLoggingEnabled;
        set
        {
            if (value == _isDebugLoggingEnabled) return;
            _isDebugLoggingEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool SaveWindowBounds
    {
        get => _saveWindowBounds;
        set
        {
            if (value == _saveWindowBounds) return;
            _saveWindowBounds = value;
            OnPropertyChanged();
        }
    }

    public double WindowTop
    {
        get => _windowTop;
        set
        {
            if (value.Equals(_windowTop)) return;
            _windowTop = value;
            OnPropertyChanged();
        }
    }

    public double WindowLeft
    {
        get => _windowLeft;
        set
        {
            if (value.Equals(_windowLeft)) return;
            _windowLeft = value;
            OnPropertyChanged();
        }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set
        {
            if (value.Equals(_windowHeight)) return;
            _windowHeight = value;
            OnPropertyChanged();
        }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (value.Equals(_windowWidth)) return;
            _windowWidth = value;
            OnPropertyChanged();
        }
    }

    public WindowState WindowState
    {
        get => _windowState;
        set
        {
            if (value == _windowState) return;
            _windowState = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}