using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevSticky.Interfaces;

namespace DevSticky.Models;

/// <summary>
/// Base class for models that support dirty tracking and property change notification
/// </summary>
public abstract class TrackableModel : ITrackable, INotifyPropertyChanged
{
    private bool _isDirty;

    /// <summary>
    /// Gets or sets whether the object has been modified since last save
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Marks the object as clean (not modified)
    /// </summary>
    public void MarkClean()
    {
        IsDirty = false;
    }

    /// <summary>
    /// Marks the object as dirty (modified)
    /// </summary>
    public void MarkDirty()
    {
        IsDirty = true;
    }

    /// <summary>
    /// Event raised when a property value changes
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets a property value and marks the object as dirty if the value changed
    /// </summary>
    /// <typeparam name="T">The type of the property</typeparam>
    /// <param name="field">Reference to the backing field</param>
    /// <param name="value">The new value</param>
    /// <param name="propertyName">Name of the property (automatically provided by compiler)</param>
    /// <returns>True if the value changed, false otherwise</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        MarkDirty();
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises the PropertyChanged event
    /// </summary>
    /// <param name="propertyName">Name of the property that changed</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
