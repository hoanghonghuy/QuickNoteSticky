using System.Windows;

namespace DevSticky.Interfaces;

/// <summary>
/// Abstraction for dialog operations to enable testability
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons
    /// </summary>
    /// <returns>True if user clicked Yes, false otherwise</returns>
    Task<bool> ShowConfirmationAsync(string title, string message);

    /// <summary>
    /// Shows an error dialog
    /// </summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>
    /// Shows an information dialog
    /// </summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Shows a success dialog
    /// </summary>
    Task ShowSuccessAsync(string title, string message);

    /// <summary>
    /// Shows a warning dialog
    /// </summary>
    Task ShowWarningAsync(string title, string message);

    /// <summary>
    /// Shows a custom dialog and returns the result
    /// </summary>
    Task<T?> ShowCustomDialogAsync<T>(Func<Window> dialogFactory) where T : class;

    /// <summary>
    /// Shows a custom dialog with a specific owner window
    /// </summary>
    Task<T?> ShowCustomDialogAsync<T>(Window owner, Func<Window> dialogFactory) where T : class;
}
