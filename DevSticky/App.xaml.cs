using System.Windows;
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

        // Setup system tray
        SetupSystemTray();
        
        // Apply initial theme to tray menu
        ApplyThemeToTrayMenu();

        // Initialize main view model and load notes
        _mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
        
        // Wire up dashboard and settings callbacks
        _mainViewModel.OnOpenDashboard = OpenDashboard;
        _mainViewModel.OnOpenSettings = OpenSettings;
        
        await _mainViewModel.LoadNotesAsync();

        // Don't show main window - notes are shown individually
        MainWindow = null;
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
        services.AddSingleton<IWindowService>(sp => 
            new WindowService(note => CreateNoteWindow(sp, note)));
        services.AddSingleton<IWindowsThemeDetector, WindowsThemeDetector>();
        services.AddSingleton<IThemeService, ThemeService>();
        
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
        _trayContextMenu.Items.Add(Services.L.Get("TraySettings"), null, (_, _) => OpenSettings());
        _trayContextMenu.Items.Add(Services.L.Get("TrayExit"), null, (_, _) => _mainViewModel?.TrayViewModel.ExitCommand.Execute(null));
        
        // Re-apply theme to new menu items
        ApplyThemeToTrayMenu();
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
        
        // Unsubscribe from language changes
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        
        _notifyIcon?.Dispose();
        _trayContextMenu?.Dispose();
        (ServiceProvider.GetService<IDebounceService>() as IDisposable)?.Dispose();
        (ServiceProvider.GetService<IThemeService>() as IDisposable)?.Dispose();
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
