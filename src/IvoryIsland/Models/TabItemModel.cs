using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IvoryIsland.Models;

/// <summary>
/// Model representing a logical application tab in the macOS Safari-inspired tab system.
/// </summary>
public sealed class TabItemModel : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _title = string.Empty;
    private string _iconGlyph = string.Empty;
    private bool _isActive;
    private bool _canClose = true;
    private string _navigationUri = string.Empty;

    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

    public string IconGlyph
    {
        get => _iconGlyph;
        set
        {
            if (_iconGlyph != value)
            {
                _iconGlyph = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanClose
    {
        get => _canClose;
        set
        {
            if (_canClose != value)
            {
                _canClose = value;
                OnPropertyChanged();
            }
        }
    }

    public string NavigationUri
    {
        get => _navigationUri;
        set
        {
            if (_navigationUri != value)
            {
                _navigationUri = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}