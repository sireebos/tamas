using System.Threading.Tasks;
using System.Windows;

namespace RestaurantManager.Services.Infrastructure;

/// <summary>
/// Modern notification service providing consistent, beautiful dialogs throughout the app
/// Uses WPF MessageBox with emojis for visual distinction
/// </summary>
public interface INotificationService
{
    Task ShowInfoAsync(string title, string message);
    Task ShowSuccessAsync(string title, string message);
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ShowConfirmAsync(string title, string message);
    Task<string?> ShowInputAsync(string title, string message, string placeholder = "");
}

public class NotificationService : INotificationService
{
    /// <summary>
    /// Shows an informational dialog
    /// </summary>
    public Task ShowInfoAsync(string title, string message)
    {
        MessageBox.Show(message, $"ℹ️ {title}", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a success dialog (green checkmark)
    /// </summary>
    public Task ShowSuccessAsync(string title, string message)
    {
        MessageBox.Show(message, $"✅ {title}", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a warning dialog (orange exclamation)
    /// </summary>
    public Task ShowWarningAsync(string title, string message)
    {
        MessageBox.Show(message, $"⚠️ {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows an error dialog (red X)
    /// </summary>
    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, $"❌ {title}", MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a confirmation dialog with Yes/No buttons
    /// </summary>
    public Task<bool> ShowConfirmAsync(string title, string message)
    {
        var result = MessageBox.Show(message, $"❓ {title}", MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>
    /// Shows a real input dialog for capturing text from user.
    /// Uses the custom InputDialog window.
    /// </summary>
    public Task<string?> ShowInputAsync(string title, string message, string initialValue = "")
    {
        string? result = null;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.Dialogs.InputDialog(title, message, initialValue);
            dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                result = dialog.InputValue;
            }
        });
        return Task.FromResult(result);
    }
}
