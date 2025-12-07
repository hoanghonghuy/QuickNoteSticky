using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using DevSticky.Views;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using WinFormsColor = System.Drawing.Color;

namespace DevSticky;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    private MainViewModel? _mainViewModel;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Windows.Forms.ContextMenuStrip? _trayContextMenu;
    private DashboardWindow? _dashboardWindow;
    private IThemeService? _themeService;
    private IHotkeyService? _hotkeyService;
    private ICloudSyncService? _cloudSyncService;
    private System.Windows.Forms.ToolStripMenuItem? _syncStatusMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _syncNowMenuItem;
    private System.Windows.Forms.ToolStripMenuItem? _lastSyncMenuItem;
    
    public static IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("ServiceProvider not initialized");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Load settings and apply language
        var settings = AppSettings.Load();
        LocalizationService.Instance.SetCulture(settings.Language);

        // Initialize and apply theme
        _themeService = ServiceProvider.GetRequiredService<IThemeService>();
        var themeMode = settings.ThemeMode switch
        {
            "Light" => Models.ThemeMode.Light,
            "Dark" => Models.ThemeMode.Dark,
            _ => Models.ThemeMode.System
        };
        _themeService.SetThemeMode(themeMode);
        
        // Subscribe to theme changes for tray menu updates (Requirements 4.1)
        _themeService.ThemeChanged += OnThemeChanged;

        // Initialize global hotkey service (Requirements 1.4)
        _hotkeyService = ServiceProvider.GetRequiredService<IHotkeyService>();
        
        // Initialize cloud sync service (Requirements 5.5, 5.6)
        try
        {
            _cloudSyncService = ServiceProvider.GetRequiredService<ICloudSyncService>();
            _cloudSyncService.SyncProgress += OnCloudSyncProgress;
        }
        catch
        {
            _cloudSyncService = null;
        }

        // Setup system tray
        SetupSystemTray();
        
        // Apply initial theme to tray menu
        ApplyThemeToTrayMenu();

        // Initialize main view model and load notes
        _mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
        
        // Wire up dashboard and settings callbacks
        _mainViewModel.OnOpenDashboard = OpenDashboard;
        _mainViewModel.OnOpenSettings = OpenSettings;
        _mainViewModel.OnShowTemplateSelection = ShowTemplateSelectionDialog;
        
        await _mainViewModel.LoadNotesAsync();

        // Register global hotkeys after main view model is ready (Requirements 1.4)
        RegisterGlobalHotkeys(settings);

        // Don't show main window - notes are shown individually
        MainWindow = null;
    }

    /// <summary>
    /// Register global hotkeys based on settings (Requirements 1.1, 1.2, 1.3, 1.4)
    /// </summary>
    private void RegisterGlobalHotkeys(AppSettings settings)
    {
        if (_hotkeyService == null) return;

        // Subscribe to hotkey events
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Register configured hotkeys
        RegisterHotkeyFromString("NewNote", settings.Hotkeys.NewNoteHotkey);
        RegisterHotkeyFromString("ToggleVisibility", settings.Hotkeys.ToggleVisibilityHotkey);
        RegisterHotkeyFromString("QuickCapture", settings.Hotkeys.QuickCaptureHotkey);
        RegisterHotkeyFromString("SnippetBrowser", settings.Hotkeys.SnippetBrowserHotkey);
    }

    /// <summary>
    /// Register a hotkey from a string configuration
    /// </summary>
    private void RegisterHotkeyFromString(string id, string hotkeyString)
    {
        if (_hotkeyService == null || string.IsNullOrEmpty(hotkeyString)) return;

        if (_hotkeyService.TryParseHotkey(hotkeyString, out var modifiers, out var key))
        {
            if (!_hotkeyService.RegisterHotkey(id, modifiers, key))
            {
                // Hotkey conflict detected (Requirements 1.6)
                NotifyHotkeyConflict(id, hotkeyString);
            }
        }
    }

    /// <summary>
    /// Notify user of hotkey conflict (Requirements 1.6)
    /// </summary>
    private void NotifyHotkeyConflict(string hotkeyId, string hotkeyString)
    {
        // Show notification via system tray balloon
        _notifyIcon?.ShowBalloonTip(
            3000,
            L.Get("HotkeyConflictTitle"),
            string.Format(L.Get("HotkeyConflictMessage"), hotkeyId, hotkeyString),
            System.Windows.Forms.ToolTipIcon.Warning);
    }

    /// <summary>
    /// Handle hotkey pressed events (Requirements 1.1, 1.2, 1.3)
    /// </summary>
    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.HotkeyId)
            {
                case "NewNote":
                    // Requirements 1.1: Create new note and bring to focus
                    _mainViewModel?.CreateNewNote();
                    break;

                case "ToggleVisibility":
                    // Requirements 1.2: Toggle visibility of all note windows
                    _mainViewModel?.TrayViewModel.ToggleVisibilityCommand?.Execute(null);
                    break;

                case "QuickCapture":
                    // Requirements 1.3: Open quick capture with clipboard content
                    OpenQuickCapture();
                    break;

                case "SnippetBrowser":
                    // Future: Open snippet browser (Phase 3)
                    break;
            }
        });
    }

    /// <summary>
    /// Open quick capture dialog with clipboard content (Requirements 1.3)
    /// </summary>
    private void OpenQuickCapture()
    {
        // Create a new note with clipboard content pre-filled
        _mainViewModel?.CreateNewNote();
        
        // Try to paste clipboard content into the new note
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var clipboardText = System.Windows.Clipboard.GetText();
                // The note window will be focused, so we can send paste command
                // For now, we just create the note - clipboard paste can be done manually
            }
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    /// <summary>
    /// Re-register hotkeys when settings change (Requirements 1.7)
    /// </summary>
    public void ReregisterHotkeys()
    {
        if (_hotkeyService == null || _mainViewModel == null) return;

        // Unregister all existing hotkeys
        _hotkeyService.UnregisterAll();

        // Re-register with current settings
        var settings = _mainViewModel.AppSettings;
        RegisterHotkeyFromString("NewNote", settings.Hotkeys.NewNoteHotkey);
        RegisterHotkeyFromString("ToggleVisibility", settings.Hotkeys.ToggleVisibilityHotkey);
        RegisterHotkeyFromString("QuickCapture", settings.Hotkeys.QuickCaptureHotkey);
        RegisterHotkeyFromString("SnippetBrowser", settings.Hotkeys.SnippetBrowserHotkey);
    }
    
    private void OpenDashboard()
    {
        if (_mainViewModel == null) return;
        
        if (_dashboardWindow == null)
        {
            _dashboardWindow = new DashboardWindow(_mainViewModel);
        }
        else
        {
            _dashboardWindow.RefreshNotesList();
        }
        
        _dashboardWindow.Show();
        _dashboardWindow.Activate();
    }
    
    private void OpenSettings()
    {
        if (_mainViewModel == null) return;
        
        var settingsWindow = new SettingsWindow(_mainViewModel.AppSettings);
        settingsWindow.ShowDialog();
    }

    /// <summary>
    /// Show template selection dialog when creating a new note (Requirements 6.1)
    /// </summary>
    private NoteTemplate? ShowTemplateSelectionDialog()
    {
        var dialog = new TemplateSelectionDialog();
        
        if (dialog.ShowDialog() == true)
        {
            if (dialog.CreateBlankNote)
            {
                // User chose blank note - return null to signal blank note creation
                return null;
            }
            return dialog.SelectedTemplate;
        }
        
        // Dialog was cancelled - create blank note as fallback
        return null;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Models
        services.AddSingleton<AppSettings>();
        
        // Services
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<INoteService>(sp => 
            new NoteService(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton<IFormatterService, FormatterService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IDebounceService, DebounceService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IWindowService>(sp => 
            new WindowService(
                note => CreateNoteWindow(sp, note),
                sp.GetRequiredService<IMonitorService>(),
                note => sp.GetRequiredService<MainViewModel>().SaveAllNotes()));
        services.AddSingleton<IWindowsThemeDetector, WindowsThemeDetector>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<ISnippetService, SnippetService>();
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<ITemplateService>(sp => 
            new TemplateService(sp.GetRequiredService<AppSettings>()));
        services.AddSingleton<ILinkService>(sp =>
            new LinkService(sp.GetRequiredService<INoteService>()));
        
        // Cloud Sync Services (Requirements 5.1, 5.11)
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<ICloudSyncService>(sp =>
            new CloudSyncService(
                sp.GetRequiredService<INoteService>(),
                sp.GetRequiredService<IStorageService>(),
                sp.GetRequiredService<IEncryptionService>()));
        
        // ViewModels
        services.AddSingleton<MainViewModel>();
    }

    private static NoteWindow CreateNoteWindow(IServiceProvider sp, Note note)
    {
        NoteWindow? window = null;
        var mainVm = sp.GetRequiredService<MainViewModel>();
        
        var vm = new NoteViewModel(
            note,
            sp.GetRequiredService<INoteService>(),
            sp.GetRequiredService<IFormatterService>(),
            sp.GetRequiredService<ISearchService>(),
            sp.GetRequiredService<IDebounceService>(),
            onClose: noteVm => 
            {
                mainVm.RemoveNote(noteVm);
                window?.Close();
            },
            onSave: () => mainVm.SaveAllNotes()
        );
        
        // Wire up NavigateToNoteCommand for internal note links (Requirements 4.7)
        vm.NavigateToNoteCommand = new RelayCommand<Guid>(noteId =>
        {
            mainVm.OpenNoteById(noteId);
        });
        
        window = new NoteWindow { DataContext = vm };
        return window;
    }


    private void SetupSystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "DevSticky",
            Visible = true
        };

        _trayContextMenu = new System.Windows.Forms.ContextMenuStrip();
        BuildTrayMenuItems();

        _notifyIcon.ContextMenuStrip = _trayContextMenu;
        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();
        
        // Subscribe to language changes to update menu text
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }
    
    /// <summary>
    /// Load the tray icon from embedded resource or fall back to system icon
    /// </summary>
    private static System.Drawing.Icon LoadTrayIcon()
    {
        try
        {
            // Try to load from Resources folder
            var iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Resources", 
                "app.ico");
            
            if (System.IO.File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
            }
            
            // Fallback to system icon if custom icon not found
            return System.Drawing.SystemIcons.Application;
        }
        catch
        {
            // Fallback to system icon on any error
            return System.Drawing.SystemIcons.Application;
        }
    }
    
    /// <summary>
    /// Build tray menu items with current language strings
    /// </summary>
    private void BuildTrayMenuItems()
    {
        if (_trayContextMenu == null) return;
        
        _trayContextMenu.Items.Clear();
        _trayContextMenu.Items.Add(Services.L.Get("TrayDashboard"), null, (_, _) => OpenDashboard());
        _trayContextMenu.Items.Add("-");
        _trayContextMenu.Items.Add(Services.L.Get("TrayNewNote"), null, (_, _) => _mainViewModel?.CreateNewNote());
        _trayContextMenu.Items.Add(Services.L.Get("TrayShowAll"), null, (_, _) => _mainViewModel?.TrayViewModel.ShowAllCommand.Execute(null));
        _trayContextMenu.Items.Add(Services.L.Get("TrayHideAll"), null, (_, _) => _mainViewModel?.TrayViewModel.HideAllCommand.Execute(null));
        _trayContextMenu.Items.Add("-");
        
        // Cloud Sync Section (Requirements 5.5, 5.6)
        AddCloudSyncMenuItems();
        
        _trayContextMenu.Items.Add(Services.L.Get("TraySettings"), null, (_, _) => OpenSettings());
        _trayContextMenu.Items.Add(Services.L.Get("TrayExit"), null, (_, _) => _mainViewModel?.TrayViewModel.ExitCommand.Execute(null));
        
        // Re-apply theme to new menu items
        ApplyThemeToTrayMenu();
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
            Text = "🔄 " + Services.L.Get("SyncNow"),
            Enabled = _cloudSyncService?.Status == SyncStatus.Idle
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
        if (_cloudSyncService == null)
            return "☁️ " + Services.L.Get("CloudSyncNotAvailable");
        
        return _cloudSyncService.Status switch
        {
            SyncStatus.Disconnected => "☁️ " + Services.L.Get("CloudStatusDisconnected"),
            SyncStatus.Connecting => "🔄 " + Services.L.Get("CloudStatusConnecting"),
            SyncStatus.Syncing => "🔄 " + Services.L.Get("CloudStatusSyncing"),
            SyncStatus.Idle => "✅ " + Services.L.Get("CloudStatusConnected"),
            SyncStatus.Error => "❌ " + Services.L.Get("CloudStatusError"),
            _ => "☁️ " + Services.L.Get("CloudStatusDisconnected")
        };
    }
    
    /// <summary>
    /// Get the last sync time text for display
    /// </summary>
    private string GetLastSyncText()
    {
        if (_cloudSyncService?.LastSyncResult?.CompletedAt != null)
        {
            var lastSync = _cloudSyncService.LastSyncResult.CompletedAt.ToLocalTime();
            return "📅 " + string.Format(Services.L.Get("LastSyncTime"), lastSync.ToString("g"));
        }
        return "📅 " + Services.L.Get("NeverSynced");
    }
    
    /// <summary>
    /// Trigger a manual sync operation (Requirements 5.5, 5.6)
    /// </summary>
    private async Task TriggerManualSync()
    {
        if (_cloudSyncService == null || _cloudSyncService.Status != SyncStatus.Idle)
            return;
        
        // Update menu to show syncing
        UpdateSyncStatusInTray();
        
        try
        {
            var result = await _cloudSyncService.SyncAsync();
            
            if (result.Success)
            {
                _notifyIcon?.ShowBalloonTip(
                    2000,
                    Services.L.Get("CloudSync"),
                    $"✅ {result.NotesUploaded} uploaded, {result.NotesDownloaded} downloaded",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
            else
            {
                _notifyIcon?.ShowBalloonTip(
                    3000,
                    Services.L.Get("CloudSync"),
                    $"❌ {result.ErrorMessage}",
                    System.Windows.Forms.ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _notifyIcon?.ShowBalloonTip(
                3000,
                Services.L.Get("Error"),
                ex.Message,
                System.Windows.Forms.ToolTipIcon.Error);
        }
        finally
        {
            UpdateSyncStatusInTray();
        }
    }
    
    /// <summary>
    /// Update the sync status display in the tray menu
    /// </summary>
    private void UpdateSyncStatusInTray()
    {
        Dispatcher.Invoke(() =>
        {
            if (_syncStatusMenuItem != null)
                _syncStatusMenuItem.Text = GetSyncStatusText();
            
            if (_lastSyncMenuItem != null)
                _lastSyncMenuItem.Text = GetLastSyncText();
            
            if (_syncNowMenuItem != null)
                _syncNowMenuItem.Enabled = _cloudSyncService?.Status == SyncStatus.Idle;
        });
    }
    
    /// <summary>
    /// Handle cloud sync progress events (Requirements 5.5, 5.6)
    /// </summary>
    private void OnCloudSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        UpdateSyncStatusInTray();
        
        // Update tray icon tooltip with progress
        Dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Text = $"DevSticky - {e.Operation}: {e.ProgressPercent}%";
            }
        });
    }
    
    /// <summary>
    /// Handle language changes and update tray menu text
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(BuildTrayMenuItems);
    }
    
    /// <summary>
    /// Handle theme changes and update tray menu styling (Requirements 4.1)
    /// </summary>
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        // Update tray menu on UI thread
        Dispatcher.Invoke(ApplyThemeToTrayMenu);
    }
    
    /// <summary>
    /// Apply current theme colors to the tray context menu (Requirements 4.1, 4.2, 4.3, 4.4)
    /// </summary>
    private void ApplyThemeToTrayMenu()
    {
        if (_trayContextMenu == null || _themeService == null)
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

    protected override void OnExit(ExitEventArgs e)
    {
        // Unsubscribe from theme changes
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
        
        // Unsubscribe from hotkey events and unregister all hotkeys (Requirements 1.5)
        if (_hotkeyService != null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.UnregisterAll();
        }
        
        // Unsubscribe from cloud sync events
        if (_cloudSyncService != null)
        {
            _cloudSyncService.SyncProgress -= OnCloudSyncProgress;
        }
        
        // Unsubscribe from language changes
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        
        _notifyIcon?.Dispose();
        _trayContextMenu?.Dispose();
        (ServiceProvider.GetService<IDebounceService>() as IDisposable)?.Dispose();
        (ServiceProvider.GetService<IThemeService>() as IDisposable)?.Dispose();
        (ServiceProvider.GetService<IHotkeyService>() as IDisposable)?.Dispose();
        (ServiceProvider.GetService<ICloudSyncService>() as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    public static T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }
}

/// <summary>
/// Custom renderer for themed tray context menu (Requirements 4.1, 4.2, 4.3, 4.4)
/// </summary>
internal class ThemedMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private readonly WinFormsColor _backgroundColor;
    private readonly WinFormsColor _textColor;
    private readonly WinFormsColor _hoverColor;
    
    public ThemedMenuRenderer(WinFormsColor backgroundColor, WinFormsColor textColor, WinFormsColor hoverColor)
        : base(new ThemedColorTable(backgroundColor, hoverColor))
    {
        _backgroundColor = backgroundColor;
        _textColor = textColor;
        _hoverColor = hoverColor;
    }
    
    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        var rect = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
        var color = e.Item.Selected ? _hoverColor : _backgroundColor;
        
        using var brush = new System.Drawing.SolidBrush(color);
        e.Graphics.FillRectangle(brush, rect);
    }
    
    protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = _textColor;
        base.OnRenderItemText(e);
    }
    
    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        var rect = new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size);
        using var brush = new System.Drawing.SolidBrush(_backgroundColor);
        e.Graphics.FillRectangle(brush, rect);
        
        // Draw separator line
        var separatorColor = WinFormsColor.FromArgb(
            Math.Min(255, _hoverColor.R + 20),
            Math.Min(255, _hoverColor.G + 20),
            Math.Min(255, _hoverColor.B + 20));
        using var pen = new System.Drawing.Pen(separatorColor);
        var y = rect.Height / 2;
        e.Graphics.DrawLine(pen, 4, y, rect.Width - 4, y);
    }
}

/// <summary>
/// Custom color table for themed tray menu
/// </summary>
internal class ThemedColorTable : System.Windows.Forms.ProfessionalColorTable
{
    private readonly WinFormsColor _backgroundColor;
    private readonly WinFormsColor _hoverColor;
    
    public ThemedColorTable(WinFormsColor backgroundColor, WinFormsColor hoverColor)
    {
        _backgroundColor = backgroundColor;
        _hoverColor = hoverColor;
    }
    
    public override WinFormsColor ToolStripDropDownBackground => _backgroundColor;
    public override WinFormsColor ImageMarginGradientBegin => _backgroundColor;
    public override WinFormsColor ImageMarginGradientMiddle => _backgroundColor;
    public override WinFormsColor ImageMarginGradientEnd => _backgroundColor;
    public override WinFormsColor MenuBorder => _hoverColor;
    public override WinFormsColor MenuItemBorder => WinFormsColor.Transparent;
    public override WinFormsColor MenuItemSelected => _hoverColor;
    public override WinFormsColor MenuItemSelectedGradientBegin => _hoverColor;
    public override WinFormsColor MenuItemSelectedGradientEnd => _hoverColor;
    public override WinFormsColor MenuStripGradientBegin => _backgroundColor;
    public override WinFormsColor MenuStripGradientEnd => _backgroundColor;
    public override WinFormsColor MenuItemPressedGradientBegin => _hoverColor;
    public override WinFormsColor MenuItemPressedGradientEnd => _hoverColor;
    public override WinFormsColor SeparatorDark => _hoverColor;
    public override WinFormsColor SeparatorLight => _backgroundColor;
}
