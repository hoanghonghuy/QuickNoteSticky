using System.Windows;
using DevSticky.Interfaces;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DevSticky.Services;

/// <summary>
/// Service for showing dialogs with proper threading support
/// </summary>
public class DialogService : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        return Task.Run(() =>
        {
            var result = MessageBoxResult.None;
            Application.Current.Dispatcher.Invoke(() =>
            {
                result = MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
            });
            return result == MessageBoxResult.Yes;
        });
    }

    public Task ShowErrorAsync(string title, string message)
    {
        return Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        });
    }

    public Task ShowInfoAsync(string title, string message)
    {
        return Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        });
    }

    public Task ShowSuccessAsync(string title, string message)
    {
        return Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        });
    }

    public Task ShowWarningAsync(string title, string message)
    {
        return Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        });
    }

    public Task<T?> ShowCustomDialogAsync<T>(Func<Window> dialogFactory) where T : class
    {
        return Task.Run(() =>
        {
            T? result = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = dialogFactory();
                dialog.ShowDialog();
                result = dialog.DataContext as T;
            });
            return result;
        });
    }

    public Task<T?> ShowCustomDialogAsync<T>(Window owner, Func<Window> dialogFactory) where T : class
    {
        return Task.Run(() =>
        {
            T? result = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = dialogFactory();
                dialog.Owner = owner;
                dialog.ShowDialog();
                result = dialog.DataContext as T;
            });
            return result;
        });
    }
}
