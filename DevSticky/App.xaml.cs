using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using DevSticky.Views;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;

namespace DevSticky;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    private MainViewModel? _mainViewModel;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private DashboardWindow? _dashboardWindow;
    private IThemeService? _themeService;
    private IHotkeyService? _hotkeyService;
    private ICloudSyncService? _cloudSyncService;
    private TrayMenuService? _trayMenuService;
    
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

        // Initialize global hotkey service (Requirements 1.4)
        _hotkeyService = ServiceProvider.GetRequiredService<IHotkeyService>();
        
        // Initialize cloud sync service (Requirements 5.5, 5.6)
        try
        {
            _cloudSyncService = ServiceProvider.GetRequiredService<ICloudSyncService>();
        }
        catch
        {
            _cloudSyncService = null;
        }

        // Setup system tray with TrayMenuService
        SetupSystemTray();

        // Initialize main view model and load notes
        _mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
        
        // Wire up dashboard and settings callbacks
        _mainViewModel.OnOpenDashboard = OpenDashboard;
        _mainViewModel.OnOpenSettings = OpenSettings;
        _mainViewModel.OnShowTemplateSelection = ShowTemplateSelectionDialog;
        
        await _mainViewModel.LoadNotesAsync();
        
        // Configure tray menu actions now that MainViewModel is ready
        ConfigureTrayMenuActions();

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
        _trayMenuService?.ShowBalloonTip(
            L.Get("HotkeyConflictTitle"),
            string.Format(L.Get("HotkeyConflictMessage"), hotkeyId, hotkeyString),
            System.Windows.Forms.ToolTipIcon.Warning,
            3000);
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
        
        // NoteWindowContext for dependency injection (Requirements 4.1, 4.2, 4.3)
        services.AddSingleton<NoteWindowContext>(sp =>
            new NoteWindowContext(
                sp.GetRequiredService<IThemeService>(),
                sp.GetRequiredService<IMonitorService>(),
                sp.GetRequiredService<ISnippetService>(),
                sp.GetRequiredService<IDebounceService>(),
                sp.GetRequiredService<IMarkdownService>(),
                sp.GetRequiredService<ILinkService>(),
                sp.GetRequiredService<INoteService>()));
        
        // ViewModels
        services.AddSingleton<MainViewModel>(sp =>
            new MainViewModel(
                sp.GetRequiredService<INoteService>(),
                sp.GetRequiredService<IStorageService>(),
                sp.GetRequiredService<IFormatterService>(),
                sp.GetRequiredService<ISearchService>(),
                sp.GetRequiredService<IDebounceService>(),
                sp.GetRequiredService<IWindowService>(),
                sp.GetRequiredService<ITemplateService>()));
    }

    private static NoteWindow CreateNoteWindow(IServiceProvider sp, Note note)
    {
        NoteWindow? window = null;
        var mainVm = sp.GetRequiredService<MainViewModel>();
        var context = sp.GetRequiredService<NoteWindowContext>();
        
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
        
        window = new NoteWindow(context) { DataContext = vm };
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

        // Create and configure TrayMenuService
        _trayMenuService = new TrayMenuService(_themeService!, _cloudSyncService, Dispatcher);
        _trayMenuService.Initialize(_notifyIcon);
        
        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();
    }
    
    /// <summary>
    /// Configure tray menu actions after MainViewModel is ready
    /// </summary>
    private void ConfigureTrayMenuActions()
    {
        if (_trayMenuService == null || _mainViewModel == null) return;
        
        _trayMenuService.ConfigureActions(
            onOpenDashboard: OpenDashboard,
            onCreateNewNote: () => _mainViewModel.CreateNewNote(),
            onShowAll: () => _mainViewModel.TrayViewModel.ShowAllCommand.Execute(null),
            onHideAll: () => _mainViewModel.TrayViewModel.HideAllCommand.Execute(null),
            onOpenSettings: OpenSettings,
            onExit: () => _mainViewModel.TrayViewModel.ExitCommand.Execute(null),
            onSyncNow: TriggerManualSync
        );
        
        _trayMenuService.BuildMenu();
        _trayMenuService.ApplyTheme();
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
    /// Trigger a manual sync operation (Requirements 5.5, 5.6)
    /// </summary>
    private async Task TriggerManualSync()
    {
        if (_cloudSyncService == null || _cloudSyncService.Status != SyncStatus.Idle)
            return;
        
        _trayMenuService?.UpdateSyncStatus(_cloudSyncService.Status, null);
        
        try
        {
            var result = await _cloudSyncService.SyncAsync();
            
            if (result.Success)
            {
                _trayMenuService?.ShowBalloonTip(
                    L.Get("CloudSync"),
                    $"✅ {result.NotesUploaded} uploaded, {result.NotesDownloaded} downloaded",
                    System.Windows.Forms.ToolTipIcon.Info,
                    2000);
            }
            else
            {
                _trayMenuService?.ShowBalloonTip(
                    L.Get("CloudSync"),
                    $"❌ {result.ErrorMessage}",
                    System.Windows.Forms.ToolTipIcon.Warning,
                    3000);
            }
        }
        catch (Exception ex)
        {
            _trayMenuService?.ShowBalloonTip(
                L.Get("Error"),
                ex.Message,
                System.Windows.Forms.ToolTipIcon.Error,
                3000);
        }
        finally
        {
            _trayMenuService?.UpdateSyncStatus(_cloudSyncService.Status, _cloudSyncService.LastSyncResult?.CompletedAt);
            _trayMenuService?.ResetTooltip();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Unsubscribe from hotkey events and unregister all hotkeys (Requirements 1.5)
        if (_hotkeyService != null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.UnregisterAll();
        }
        
        // Dispose tray menu service (handles its own event unsubscription)
        _trayMenuService?.Dispose();
        
        // Clean up dashboard window event subscriptions
        _dashboardWindow?.Shutdown();
        
        _notifyIcon?.Dispose();
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
