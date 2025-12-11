using System.Windows.Threading;
using DevSticky.Interfaces;
using DevSticky.Models;
using WinFormsColor = System.Drawing.Color;

namespace DevSticky.Services;

/// <summary>
/// Service for managing the system tray icon and context menu.
/// Handles menu building, theme application, and sync status updates.
/// </summary>
public class TrayMenuService : ITrayMenuService
{
    private readonly IThemeService _themeService;
    private readonly ICloudConnection? _cloudConnection;
    private readonly ICloudSync? _cloudSync;
    private readonly Dispatcher _dispatcher;
    
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Windows.Forms.ContextMenuStrip? _trayContextMenu;
    private System.Windows.Forms.ToolStripMenuItem? _syncStatusMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _syncNowMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _lastSyncMenuItem;
    
    // Callbacks for menu actions
    private Action? _onOpenDashboard;
    private Action? _onCreateNewNote;
    private Action? _onShowAll;
    private Action? _onHideAll;
    private Action? _onOpenSettings;
    private Action? _onExit;
    private Func<Task>? _onSyncNow;
    
    private bool _disposed;

    public TrayMenuService(
        IThemeService themeService,
        ICloudConnection? cloudConnection,
        ICloudSync? cloudSync,
        Dispatcher dispatcher)
    {
        _themeService = themeService;
        _cloudConnection = cloudConnection;
        _cloudSync = cloudSync;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Configure the menu action callbacks.
    /// </summary>
    public void ConfigureActions(
        Action onOpenDashboard,
        Action onCreateNewNote,
        Action onShowAll,
        Action onHideAll,
        Action onOpenSettings,
        Action onExit,
        Func<Task>? onSyncNow = null)
    {
        _onOpenDashboard = onOpenDashboard;
        _onCreateNewNote = onCreateNewNote;
        _onShowAll = onShowAll;
        _onHideAll = onHideAll;
        _onOpenSettings = onOpenSettings;
        _onExit = onExit;
        _onSyncNow = onSyncNow;
    }

    /// <inheritdoc />
    public void Initialize(System.Windows.Forms.NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
        _trayContextMenu = new System.Windows.Forms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip = _trayContextMenu;
        
        // Subscribe to language changes to update menu text
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        
        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;
        
        // Subscribe to cloud sync progress if available
        if (_cloudSync != null)
        {
            _cloudSync.SyncProgress += OnCloudSyncProgress;
        }
    }

    /// <inheritdoc />
    public void BuildMenu()
    {
        if (_trayContextMenu == null) return;
        
        _trayContextMenu.Items.Clear();
        _trayContextMenu.Items.Add(L.Get("TrayDashboard"), null, (_, _) => _onOpenDashboard?.Invoke());
        _trayContextMenu.Items.Add("-");
        _trayContextMenu.Items.Add(L.Get("TrayNewNote"), null, (_, _) => _onCreateNewNote?.Invoke());
        _trayContextMenu.Items.Add(L.Get("TrayShowAll"), null, (_, _) => _onShowAll?.Invoke());
        _trayContextMenu.Items.Add(L.Get("TrayHideAll"), null, (_, _) => _onHideAll?.Invoke());
        _trayContextMenu.Items.Add("-");
        
        // Cloud Sync Section (Requirements 5.5, 5.6)
        AddCloudSyncMenuItems();
        
        _trayContextMenu.Items.Add(L.Get("TraySettings"), null, (_, _) => _onOpenSettings?.Invoke());
        _trayContextMenu.Items.Add(L.Get("TrayExit"), null, (_, _) => _onExit?.Invoke());
        
        // Re-apply theme to new menu items
        ApplyTheme();
    }

    /// <summary>
    /// Add cloud sync menu items to the tray context menu (Requirements 5.5, 5.6)
    /// </summary>
    private void AddCloudSyncMenuItems()
    {
        if (_trayContextMenu == null) return;
        
        // Sync status indicator
        _syncStatusMenuItem = new System.Windows.Forms.ToolStripMenuItem
        {
            Text = GetSyncStatusText(),
            Enabled = false // Status is display-only
        };
        _trayContextMenu.Items.Add(_syncStatusMenuItem);
        
        // Last sync time
        _lastSyncMenuItem = new System.Windows.Forms.ToolStripMenuItem
        {
            Text = GetLastSyncText(),
            Enabled = false // Status is display-only
        };
        _trayContextMenu.Items.Add(_lastSyncMenuItem);
        
        // Sync Now button
        _syncNowMenuItem = new System.Windows.Forms.ToolStripMenuItem
        {
            Text = "🔄 " + L.Get("SyncNow"),
            Enabled = _cloudConnection?.Status == SyncStatus.Idle
        };
        _syncNowMenuItem.Click += async (_, _) => await TriggerManualSync();
        _trayContextMenu.Items.Add(_syncNowMenuItem);
        
        _trayContextMenu.Items.Add("-");
    }

    /// <summary>
    /// Get the sync status text for display
    /// </summary>
    private string GetSyncStatusText()
    {
        if (_cloudConnection == null)
            return "☁️ " + L.Get("CloudSyncNotAvailable");
        
        return _cloudConnection.Status switch
        {
            SyncStatus.Disconnected => "☁️ " + L.Get("CloudStatusDisconnected"),
            SyncStatus.Connecting => "🔄 " + L.Get("CloudStatusConnecting"),
            SyncStatus.Syncing => "🔄 " + L.Get("CloudStatusSyncing"),
            SyncStatus.Idle => "✅ " + L.Get("CloudStatusConnected"),
            SyncStatus.Error => "❌ " + L.Get("CloudStatusError"),
            _ => "☁️ " + L.Get("CloudStatusDisconnected")
        };
    }

    /// <summary>
    /// Get the last sync time text for display
    /// </summary>
    private string GetLastSyncText()
    {
        if (_cloudSync?.LastSyncResult?.CompletedAt != null)
        {
            var lastSync = _cloudSync.LastSyncResult.CompletedAt.ToLocalTime();
            return "📅 " + string.Format(L.Get("LastSyncTime"), lastSync.ToString("g", LocalizationService.Instance.CurrentCulture));
        }
        return "📅 " + L.Get("NeverSynced");
    }

    /// <summary>
    /// Trigger a manual sync operation (Requirements 5.5, 5.6)
    /// </summary>
    private async Task TriggerManualSync()
    {
        if (_cloudSync == null || _cloudConnection?.Status != SyncStatus.Idle)
            return;
        
        // Update menu to show syncing
        UpdateSyncStatusInTray();
        
        try
        {
            if (_onSyncNow != null)
            {
                await _onSyncNow();
            }
            else
            {
                var result = await _cloudSync.SyncAsync();
                
                if (result.Success)
                {
                    ShowBalloonTip(
                        L.Get("CloudSync"),
                        $"✅ {result.NotesUploaded} uploaded, {result.NotesDownloaded} downloaded",
                        System.Windows.Forms.ToolTipIcon.Info,
                        2000);
                }
                else
                {
                    ShowBalloonTip(
                        L.Get("CloudSync"),
                        $"❌ {result.ErrorMessage}",
                        System.Windows.Forms.ToolTipIcon.Warning,
                        3000);
                }
            }
        }
        catch (Exception ex)
        {
            ShowBalloonTip(
                L.Get("Error"),
                ex.Message,
                System.Windows.Forms.ToolTipIcon.Error,
                3000);
        }
        finally
        {
            UpdateSyncStatusInTray();
        }
    }

    /// <inheritdoc />
    public void UpdateSyncStatus(SyncStatus status, DateTime? lastSync)
    {
        _dispatcher.Invoke(UpdateSyncStatusInTray);
    }

    /// <summary>
    /// Update the sync status display in the tray menu
    /// </summary>
    private void UpdateSyncStatusInTray()
    {
        _dispatcher.Invoke(() =>
        {
            if (_syncStatusMenuItem != null)
                _syncStatusMenuItem.Text = GetSyncStatusText();
            
            if (_lastSyncMenuItem != null)
                _lastSyncMenuItem.Text = GetLastSyncText();
            
            if (_syncNowMenuItem != null)
                _syncNowMenuItem.Enabled = _cloudConnection?.Status == SyncStatus.Idle;
        });
    }

    /// <inheritdoc />
    public void ApplyTheme()
    {
        if (_trayContextMenu == null)
            return;
        
        // Get theme colors from resources
        var backgroundColor = _themeService.GetColor("Base");
        var textColor = _themeService.GetColor("Text");
        var hoverColor = _themeService.GetColor("Surface1");
        
        // Convert WPF colors to WinForms colors
        var winFormsBgColor = WinFormsColor.FromArgb(backgroundColor.A, backgroundColor.R, backgroundColor.G, backgroundColor.B);
        var winFormsTextColor = WinFormsColor.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B);
        var winFormsHoverColor = WinFormsColor.FromArgb(hoverColor.A, hoverColor.R, hoverColor.G, hoverColor.B);
        
        // Apply custom renderer for themed appearance
        _trayContextMenu.Renderer = new ThemedMenuRenderer(winFormsBgColor, winFormsTextColor, winFormsHoverColor);
        
        // Apply colors to menu items
        foreach (System.Windows.Forms.ToolStripItem item in _trayContextMenu.Items)
        {
            if (item is System.Windows.Forms.ToolStripMenuItem menuItem)
            {
                menuItem.ForeColor = winFormsTextColor;
                menuItem.BackColor = winFormsBgColor;
            }
        }
    }

    /// <inheritdoc />
    public void ShowBalloonTip(string title, string message, System.Windows.Forms.ToolTipIcon icon, int timeout = 3000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
    }

    /// <summary>
    /// Handle language changes and update tray menu text
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _dispatcher.Invoke(BuildMenu);
    }

    /// <summary>
    /// Handle theme changes and update tray menu styling (Requirements 4.1)
    /// </summary>
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _dispatcher.Invoke(ApplyTheme);
    }

    /// <summary>
    /// Handle cloud sync progress events (Requirements 5.5, 5.6)
    /// </summary>
    private void OnCloudSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        UpdateSyncStatusInTray();
        
        // Update tray icon tooltip with progress
        _dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"DevSticky - {e.Operation}: {e.ProgressPercent}%";
            }
        });
    }

    /// <summary>
    /// Reset the tray icon tooltip to default
    /// </summary>
    public void ResetTooltip()
    {
        _dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = "DevSticky";
            }
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        
        if (disposing)
        {
            // Unsubscribe from events
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            _themeService.ThemeChanged -= OnThemeChanged;
            
            if (_cloudSync != null)
            {
                _cloudSync.SyncProgress -= OnCloudSyncProgress;
            }
            
            _trayContextMenu?.Dispose();
        }
    }
}
