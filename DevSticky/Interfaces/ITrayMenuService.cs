namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing the system tray icon and context menu.
/// Handles menu building, theme application, and sync status updates.
/// </summary>
public interface ITrayMenuService : IDisposable
{
    /// <summary>
    /// Initialize the tray menu service with the notify icon.
    /// </summary>
    /// <param name="notifyIcon">The system tray notify icon.</param>
    void Initialize(System.Windows.Forms.NotifyIcon notifyIcon);

    /// <summary>
    /// Build or rebuild the tray context menu items.
    /// </summary>
    void BuildMenu();

    /// <summary>
    /// Apply the current theme colors to the tray menu.
    /// </summary>
    void ApplyTheme();

    /// <summary>
    /// Update the sync status display in the tray menu.
    /// </summary>
    /// <param name="status">The current sync status.</param>
    /// <param name="lastSync">The last sync time, if available.</param>
    void UpdateSyncStatus(SyncStatus status, DateTime? lastSync);

    /// <summary>
    /// Show a balloon notification from the tray icon.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="icon">The notification icon type.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    void ShowBalloonTip(string title, string message, System.Windows.Forms.ToolTipIcon icon, int timeout = 3000);
}
