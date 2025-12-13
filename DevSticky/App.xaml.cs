using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private IStartupDiagnostics? _startupDiagnostics;
    private IStartupValidator? _startupValidator;
    private IRecoveryManager? _recoveryManager;
    private ISafeModeController? _safeModeController;
    private CrashErrorDialogService? _crashErrorDialogService;
    private IPerformanceMonitoringService? _performanceMonitoringService;
    
    public static IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("ServiceProvider not initialized");

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Initialize crash fix components early
        IExceptionLogger? exceptionLogger = null;
        IStartupDiagnostics? startupDiagnostics = null;
        IStartupValidator? startupValidator = null;
        ISafeModeController? safeModeController = null;
        IPerformanceMonitoringService? performanceMonitoringService = null;
        
        try
        {
            base.OnStartup(e);
            
            // Phase 0: Initialize Crash Fix Infrastructure
            var crashFixStep = StartupStep.Start("InitializeCrashFixInfrastructure", "CrashFix", "Initialization");
            
            try
            {
                // Initialize basic crash fix services without DI
                startupDiagnostics = new StartupDiagnostics();
                _startupDiagnostics = startupDiagnostics;
                startupDiagnostics.IsVerboseLoggingEnabled = true;
                
                // Initialize performance monitoring service
                performanceMonitoringService = new PerformanceMonitoringService(startupDiagnostics);
                _performanceMonitoringService = performanceMonitoringService;
                
                // Start timing crash fix infrastructure
                performanceMonitoringService.StartCategoryTiming("CrashFixInfrastructure");
                
                safeModeController = new SafeModeController();
                _safeModeController = safeModeController;
                
                startupValidator = new StartupValidator();
                _startupValidator = startupValidator;
                
                performanceMonitoringService.StopCategoryTiming("CrashFixInfrastructure");
                startupDiagnostics.CompleteStep(crashFixStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService?.StopCategoryTiming("CrashFixInfrastructure");
                startupDiagnostics?.FailStep(crashFixStep, ex);
                throw;
            }
            
            // Phase 1: Early Validation and Safe Mode Check
            var validationStep = startupDiagnostics.StartStep("EarlyValidation", "StartupValidator", "PreValidation");
            
            try
            {
                // Start timing validation overhead
                performanceMonitoringService.StartCategoryTiming("Validation");
                
                // Perform early validation without service dependencies
                var earlyValidationResult = startupValidator.ValidateDirectories();
                earlyValidationResult.Merge(startupValidator.ValidateDependencies());
                
                // Check if safe mode should be activated based on validation results
                var criticalIssues = earlyValidationResult.Issues
                    .Where(i => i.Severity == ValidationSeverity.Critical)
                    .ToList();
                
                if (safeModeController.ShouldActivateSafeMode(criticalIssues))
                {
                    safeModeController.ActivateSafeMode("Critical validation failures detected during early startup");
                }
                
                performanceMonitoringService.StopCategoryTiming("Validation");
                startupDiagnostics.CompleteStep(validationStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("Validation");
                startupDiagnostics.FailStep(validationStep, ex);
                // Continue startup even if early validation fails
            }
            
            // Phase 2: Service Registration and DI Container Setup
            var serviceRegistrationStep = startupDiagnostics.StartStep("ServiceRegistration", "DependencyInjection", "ServiceSetup");
            
            try
            {
                performanceMonitoringService.StartCategoryTiming("ServiceInitialization");
                
                var services = new ServiceCollection();
                ConfigureServices(services);
                
                // Register crash fix services in DI container
                services.AddSingleton<IStartupDiagnostics>(startupDiagnostics);
                services.AddSingleton<IStartupValidator>(sp => new StartupValidator(sp, sp.GetRequiredService<IFileSystem>()));
                services.AddSingleton<IRecoveryManager, RecoveryManager>();
                services.AddSingleton<ISafeModeController>(safeModeController);
                services.AddSingleton<CrashErrorDialogService>();
                services.AddSingleton<IPerformanceMonitoringService>(performanceMonitoringService);
                
                _serviceProvider = services.BuildServiceProvider();
                
                // Update crash fix services with DI-enabled versions
                _startupValidator = ServiceProvider.GetRequiredService<IStartupValidator>();
                _recoveryManager = ServiceProvider.GetRequiredService<IRecoveryManager>();
                _crashErrorDialogService = ServiceProvider.GetRequiredService<CrashErrorDialogService>();
                
                // Mark first service milestone
                performanceMonitoringService.MarkMilestone("FirstService");
                performanceMonitoringService.StopCategoryTiming("ServiceInitialization");
                startupDiagnostics.CompleteStep(serviceRegistrationStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("ServiceInitialization");
                startupDiagnostics.FailStep(serviceRegistrationStep, ex);
                throw;
            }
            
            // Phase 3: Initialize Exception Logger
            var exceptionLoggerStep = startupDiagnostics.StartStep("ExceptionLoggerInitialization", "ExceptionLogger", "ServiceInitialization");
            
            try
            {
                exceptionLogger = ServiceProvider.GetRequiredService<IExceptionLogger>();
                startupDiagnostics.CompleteStep(exceptionLoggerStep);
            }
            catch (Exception ex)
            {
                startupDiagnostics.FailStep(exceptionLoggerStep, ex);
                throw;
            }
            
            // Phase 4: Comprehensive Startup Validation
            var comprehensiveValidationStep = startupDiagnostics.StartStep("ComprehensiveValidation", "StartupValidator", "FullValidation");
            
            try
            {
                performanceMonitoringService.StartCategoryTiming("Validation");
                
                var validationResult = await _startupValidator.ValidateAsync();
                
                // Check for critical issues that require recovery
                var criticalIssues = validationResult.Issues
                    .Where(i => i.Severity == ValidationSeverity.Critical || i.Severity == ValidationSeverity.Error)
                    .ToList();
                
                if (criticalIssues.Count > 0)
                {
                    // Attempt automatic recovery
                    var recoveryResults = await _recoveryManager.PerformComprehensiveRecoveryAsync();
                    var recoverySuccessful = recoveryResults.All(r => r.IsSuccessful);
                    
                    if (!recoverySuccessful && safeModeController.ShouldActivateSafeMode(criticalIssues))
                    {
                        safeModeController.ActivateSafeMode("Automatic recovery failed for critical startup issues");
                    }
                }
                
                performanceMonitoringService.StopCategoryTiming("Validation");
                startupDiagnostics.CompleteStep(comprehensiveValidationStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("Validation");
                startupDiagnostics.FailStep(comprehensiveValidationStep, ex);
                // Continue startup but activate safe mode
                safeModeController.ActivateSafeMode($"Validation failed with exception: {ex.Message}");
            }
            
            // Phase 5: Settings and Localization
            var settingsStep = startupDiagnostics.StartStep("SettingsAndLocalization", "AppSettings", "Configuration");
            
            AppSettings settings;
            try
            {
                performanceMonitoringService.StartCategoryTiming("ConfigurationLoading");
                
                settings = startupDiagnostics.ExecuteStep("LoadSettings", () =>
                {
                    var loadedSettings = AppSettings.Load();
                    LocalizationService.Instance.SetCulture(loadedSettings.Language);
                    return loadedSettings;
                }, "AppSettings", "Configuration");
                
                performanceMonitoringService.StopCategoryTiming("ConfigurationLoading");
                startupDiagnostics.CompleteStep(settingsStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("ConfigurationLoading");
                startupDiagnostics.FailStep(settingsStep, ex);
                
                // Use default settings if loading fails
                settings = new AppSettings();
                await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                {
                    Phase = "SettingsAndLocalization",
                    Component = "AppSettings",
                    Operation = "LoadAndApplyLanguage"
                });
            }

            // Phase 6: Theme Initialization
            var themeStep = startupDiagnostics.StartStep("ThemeInitialization", "ThemeService", "ServiceInitialization");
            
            try
            {
                performanceMonitoringService.StartCategoryTiming("ResourceLoading");
                
                _themeService = startupDiagnostics.ExecuteStep("InitializeTheme", () =>
                {
                    var themeService = ServiceProvider.GetRequiredService<IThemeService>();
                    if (themeService is IDisposable disposableThemeService)
                    {
                        exceptionLogger.TrackResource(disposableThemeService);
                    }
                    
                    // Use default theme in safe mode
                    var themeMode = safeModeController.IsInSafeMode ? Models.ThemeMode.System : settings.ThemeMode switch
                    {
                        "Light" => Models.ThemeMode.Light,
                        "Dark" => Models.ThemeMode.Dark,
                        _ => Models.ThemeMode.System
                    };
                    themeService.SetThemeMode(themeMode);
                    return themeService;
                }, "ThemeService", "ServiceInitialization");
                
                performanceMonitoringService.StopCategoryTiming("ResourceLoading");
                startupDiagnostics.CompleteStep(themeStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("ResourceLoading");
                startupDiagnostics.FailStep(themeStep, ex);
                await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                {
                    Phase = "ThemeInitialization",
                    Component = "ThemeService",
                    Operation = "SetThemeMode"
                });
            }

            // Phase 7: Cloud Sync Service Initialization (Optional - Skip in Safe Mode)
            if (!safeModeController.IsInSafeMode)
            {
                var cloudSyncStep = startupDiagnostics.StartStep("CloudSyncInitialization", "CloudSyncService", "OptionalServices");
                
                try
                {
                    performanceMonitoringService.StartCategoryTiming("OptionalServices");
                    
                    _cloudSyncService = startupDiagnostics.ExecuteStep("InitializeCloudSync", () =>
                    {
                        var cloudService = ServiceProvider.GetRequiredService<ICloudSyncService>();
                        if (cloudService is IDisposable disposableCloudService)
                        {
                            exceptionLogger.TrackResource(disposableCloudService);
                        }
                        return cloudService;
                    }, "CloudSyncService", "OptionalServices");
                    
                    performanceMonitoringService.StopCategoryTiming("OptionalServices");
                    startupDiagnostics.CompleteStep(cloudSyncStep);
                }
                catch (Exception ex)
                {
                    performanceMonitoringService.StopCategoryTiming("OptionalServices");
                    startupDiagnostics.FailStep(cloudSyncStep, ex);
                    // Cloud sync is optional - log but continue startup
                    await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                    {
                        Phase = "CloudSyncInitialization",
                        Component = "CloudSyncService",
                        Operation = "Initialize"
                    });
                    _cloudSyncService = null;
                }
            }

            // Phase 8: System Tray Setup
            var trayStep = startupDiagnostics.StartStep("SystemTraySetup", "TrayMenuService", "UIInitialization");
            
            try
            {
                performanceMonitoringService.StartCategoryTiming("UISetup");
                
                startupDiagnostics.ExecuteStep("SetupSystemTray", () =>
                {
                    SetupSystemTray();
                    return true;
                }, "TrayMenuService", "UIInitialization");
                
                // Mark UI ready milestone
                performanceMonitoringService.MarkMilestone("UIReady");
                performanceMonitoringService.StopCategoryTiming("UISetup");
                startupDiagnostics.CompleteStep(trayStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("UISetup");
                startupDiagnostics.FailStep(trayStep, ex);
                await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                {
                    Phase = "SystemTraySetup",
                    Component = "TrayMenuService",
                    Operation = "SetupSystemTray"
                });
            }

            // Phase 9: Main ViewModel Initialization
            var viewModelStep = startupDiagnostics.StartStep("MainViewModelInitialization", "MainViewModel", "ViewModelSetup");
            
            try
            {
                _mainViewModel = startupDiagnostics.ExecuteStep("InitializeMainViewModel", () =>
                {
                    var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
                    
                    // Wire up dashboard and settings callbacks
                    mainViewModel.OnOpenDashboard = OpenDashboard;
                    mainViewModel.OnOpenSettings = OpenSettings;
                    mainViewModel.OnShowTemplateSelection = ShowTemplateSelectionDialog;
                    
                    return mainViewModel;
                }, "MainViewModel", "ViewModelSetup");
                
                startupDiagnostics.CompleteStep(viewModelStep);
            }
            catch (Exception ex)
            {
                startupDiagnostics.FailStep(viewModelStep, ex);
                throw; // Main ViewModel is critical
            }

            // Phase 10: Notes Loading (Limited in Safe Mode)
            var notesStep = startupDiagnostics.StartStep("NotesLoading", "MainViewModel", "DataLoading");
            
            try
            {
                performanceMonitoringService.StartCategoryTiming("DataLoading");
                
                await startupDiagnostics.ExecuteStepAsync("LoadNotes", async () =>
                {
                    await _mainViewModel.LoadNotesAsync();
                    return true;
                }, "MainViewModel", "DataLoading");
                
                performanceMonitoringService.StopCategoryTiming("DataLoading");
                startupDiagnostics.CompleteStep(notesStep);
            }
            catch (Exception ex)
            {
                performanceMonitoringService.StopCategoryTiming("DataLoading");
                startupDiagnostics.FailStep(notesStep, ex);
                await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                {
                    Phase = "NotesLoading",
                    Component = "MainViewModel",
                    Operation = "LoadNotesAsync"
                });
            }

            // Phase 11: Tray Menu Configuration
            var trayMenuStep = startupDiagnostics.StartStep("TrayMenuConfiguration", "TrayMenuService", "UIConfiguration");
            
            try
            {
                startupDiagnostics.ExecuteStep("ConfigureTrayMenu", () =>
                {
                    ConfigureTrayMenuActions();
                    return true;
                }, "TrayMenuService", "UIConfiguration");
                
                startupDiagnostics.CompleteStep(trayMenuStep);
            }
            catch (Exception ex)
            {
                startupDiagnostics.FailStep(trayMenuStep, ex);
                await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                {
                    Phase = "TrayMenuConfiguration",
                    Component = "TrayMenuService",
                    Operation = "ConfigureTrayMenuActions"
                });
            }

            // Phase 12: Hotkey Service Initialization (Skip in Safe Mode)
            if (!safeModeController.IsInSafeMode)
            {
                var hotkeyStep = startupDiagnostics.StartStep("HotkeyServiceInitialization", "HotkeyService", "OptionalServices");
                
                try
                {
                    performanceMonitoringService.StartCategoryTiming("OptionalServices");
                    
                    _hotkeyService = startupDiagnostics.ExecuteStep("InitializeHotkeyService", () =>
                    {
                        var hotkeyService = ServiceProvider.GetRequiredService<IHotkeyService>();
                        if (hotkeyService is IDisposable disposableHotkeyService)
                        {
                            exceptionLogger.TrackResource(disposableHotkeyService);
                        }
                        return hotkeyService;
                    }, "HotkeyService", "OptionalServices");
                    
                    startupDiagnostics.CompleteStep(hotkeyStep);
                }
                catch (Exception ex)
                {
                    startupDiagnostics.FailStep(hotkeyStep, ex);
                    await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                    {
                        Phase = "HotkeyServiceInitialization",
                        Component = "HotkeyService",
                        Operation = "Initialize"
                    });
                }

                // Phase 13: Global Hotkeys Registration (Skip in Safe Mode)
                if (_hotkeyService != null)
                {
                    var hotkeyRegistrationStep = startupDiagnostics.StartStep("GlobalHotkeysRegistration", "HotkeyService", "OptionalServices");
                    
                    try
                    {
                        startupDiagnostics.ExecuteStep("RegisterGlobalHotkeys", () =>
                        {
                            RegisterGlobalHotkeys(settings);
                            return true;
                        }, "HotkeyService", "OptionalServices");
                        
                        startupDiagnostics.CompleteStep(hotkeyRegistrationStep);
                    }
                    catch (Exception ex)
                    {
                        startupDiagnostics.FailStep(hotkeyRegistrationStep, ex);
                        await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                        {
                            Phase = "GlobalHotkeysRegistration",
                            Component = "HotkeyService",
                            Operation = "RegisterGlobalHotkeys"
                        });
                    }
                }
                
                performanceMonitoringService.StopCategoryTiming("OptionalServices");
            }

            // Phase 14: Final Safe Mode Configuration
            if (safeModeController.IsInSafeMode)
            {
                var safeModeConfigStep = startupDiagnostics.StartStep("SafeModeConfiguration", "SafeModeController", "SafeModeSetup");
                
                try
                {
                    startupDiagnostics.ExecuteStep("ConfigureSafeMode", () =>
                    {
                        safeModeController.ConfigureMinimalServices(_serviceProvider);
                        return true;
                    }, "SafeModeController", "SafeModeSetup");
                    
                    startupDiagnostics.CompleteStep(safeModeConfigStep);
                }
                catch (Exception ex)
                {
                    startupDiagnostics.FailStep(safeModeConfigStep, ex);
                    await exceptionLogger.LogStartupExceptionAsync(ex, new StartupExceptionContext
                    {
                        Phase = "SafeModeConfiguration",
                        Component = "SafeModeController",
                        Operation = "ConfigureMinimalServices"
                    });
                }
            }

            // Phase 15: Startup Completion
            var completionStep = startupDiagnostics.StartStep("StartupCompletion", "Application", "Finalization");
            
            try
            {
                // Mark fully functional milestone
                performanceMonitoringService.MarkMilestone("FullyFunctional");
                
                // Log startup summary and performance metrics
                startupDiagnostics.LogStartupSummary();
                performanceMonitoringService.LogPerformanceSummary();
                
                // Export performance metrics if needed (for debugging/analysis)
                var metricsPath = Path.Combine(Path.GetTempPath(), $"DevSticky_StartupMetrics_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                await performanceMonitoringService.ExportPerformanceMetricsAsync(metricsPath);
                
                // Don't show main window - notes are shown individually
                MainWindow = null;
                
                startupDiagnostics.CompleteStep(completionStep);
            }
            catch (Exception ex)
            {
                startupDiagnostics.FailStep(completionStep, ex);
                // Continue even if completion logging fails
            }
        }
        catch (Exception ex)
        {
            // Critical startup failure - attempt recovery or show error dialog
            var handled = await HandleCriticalStartupFailure(ex, exceptionLogger, startupDiagnostics, safeModeController);
            
            if (!handled)
            {
                Environment.Exit(1);
            }
        }
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
        // Hotkey events may come from background threads, use helper for proper STA thread execution
        InvokeOnUIThread(() =>
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
        
        // Use InvokeOnUIThread helper to ensure proper STA thread execution
        InvokeOnUIThread(() =>
        {
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
        });
    }
    
    private void OpenSettings()
    {
        if (_mainViewModel == null) return;
        
        // Use InvokeOnUIThread helper to ensure proper STA thread execution
        InvokeOnUIThread(() =>
        {
            var settingsWindow = new SettingsWindow(_mainViewModel.AppSettings);
            settingsWindow.ShowDialog();
        });
    }

    /// <summary>
    /// Show template selection dialog when creating a new note (Requirements 6.1)
    /// </summary>
    private NoteTemplate? ShowTemplateSelectionDialog()
    {
        // Use InvokeOnUIThread helper to ensure proper STA thread execution
        return InvokeOnUIThread(() =>
        {
            var templateService = ServiceProvider.GetRequiredService<ITemplateService>();
            var dialog = new TemplateSelectionDialog(templateService);
            
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
        });
    }
    
    /// <summary>
    /// Helper method to safely invoke actions on the WPF UI thread.
    /// Handles cross-thread calls from WinForms tray menu events.
    /// </summary>
    private static void InvokeOnUIThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            action();
            return;
        }
        
        if (dispatcher.CheckAccess())
        {
            // Already on UI thread
            action();
        }
        else
        {
            // Marshal to UI thread with high priority
            dispatcher.Invoke(action, DispatcherPriority.Normal);
        }
    }
    
    /// <summary>
    /// Helper method to safely invoke functions on the WPF UI thread and return a value.
    /// Handles cross-thread calls from WinForms tray menu events.
    /// </summary>
    private static T? InvokeOnUIThread<T>(Func<T?> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return func();
        }
        
        if (dispatcher.CheckAccess())
        {
            // Already on UI thread
            return func();
        }
        else
        {
            // Marshal to UI thread with high priority
            return dispatcher.Invoke(func, DispatcherPriority.Normal);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Models - Singleton (shared application state)
        services.AddSingleton<AppSettings>();
        
        // Abstraction Layer Services - Singleton (stateless utilities)
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IErrorHandler, ErrorHandler>();
        services.AddSingleton<IExceptionLogger, ExceptionLogger>();
        
        // Core Services - Singleton (shared application state)
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<INoteService, NoteService>();
        
        // Utility Services - Singleton (stateless)
        services.AddSingleton<IFormatterService, FormatterService>();
        services.AddSingleton<ISearchService, SearchService>();
        
        // System Services - Singleton (system resources)
        services.AddSingleton<IDebounceService, DebounceService>();
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IWindowsThemeDetector, WindowsThemeDetector>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        
        // Content Services - Singleton (stateless)
        services.AddSingleton<ISnippetService, SnippetService>();
        services.AddSingleton<IMarkdownService, MarkdownService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddSingleton<ILinkService, LinkService>();
        services.AddSingleton<IFileDropService, FileDropService>();
        services.AddSingleton<IFuzzySearchService, FuzzySearchService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<ISmartCollectionService, SmartCollectionService>();
        services.AddSingleton<IKanbanService, KanbanService>();
        // ITagManagementService is created by MainViewModel, so we get it from there
        services.AddSingleton<ITagManagementService>(sp => sp.GetRequiredService<MainViewModel>().TagManagementService);
        services.AddSingleton<ITimelineService, TimelineService>();
        
        // Cache Services - Singleton (shared cache) - Enhanced cache for future use
        services.AddSingleton<ILruCache<Guid, NoteTag>>(sp => new LruCache<Guid, NoteTag>(100));
        services.AddSingleton<ILruCache<Guid, NoteGroup>>(sp => new LruCache<Guid, NoteGroup>(50));
        services.AddSingleton<ICacheService, EnhancedCacheService>();
        
        // Performance Services - Singleton (shared queues)
        services.AddSingleton<ISaveQueueService, SaveQueueService>();
        services.AddSingleton<IDirtyTracker<Note>>(sp => new DirtyTracker<Note>());
        
        // v2.1 Services - Memory Management and User Experience
        services.AddSingleton<IMemoryCleanupService, MemoryCleanupService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IRecentNotesService, RecentNotesService>();
        services.AddTransient<IUndoRedoService, UndoRedoService>();
        
        // Window Services - Transient (per-window instances)
        services.AddTransient<IWindowService>(sp => 
            new WindowService(
                note => CreateNoteWindow(sp, note),
                sp.GetRequiredService<IMonitorService>(),
                note => sp.GetRequiredService<MainViewModel>().SaveAllNotes()));
        
        // Cloud Sync Services - Singleton (shared connection state)
        services.AddSingleton<IEncryptionService, EncryptionService>();
        
        // Register Cloud Provider Registry (OCP) - Singleton
        services.AddSingleton<ICloudProviderRegistry>(sp =>
        {
            var registry = new CloudProviderRegistry();
            
            // Register OneDrive provider
            registry.RegisterProvider(CloudProvider.OneDrive, () => new OneDriveStorageProvider());
            
            // Register GoogleDrive provider
            registry.RegisterProvider(CloudProvider.GoogleDrive, () => new GoogleDriveStorageProvider());
            
            return registry;
        });
        services.AddSingleton<ICloudSyncService, CloudSyncService>();
        
        // Register segregated interfaces (ISP) - all point to the same singleton instance
        services.AddSingleton<ICloudConnection>(sp => 
            (ICloudConnection)sp.GetRequiredService<ICloudSyncService>());
        services.AddSingleton<ICloudSync>(sp => 
            (ICloudSync)sp.GetRequiredService<ICloudSyncService>());
        services.AddSingleton<ICloudConflictResolver>(sp => 
            (ICloudConflictResolver)sp.GetRequiredService<ICloudSyncService>());
        services.AddSingleton<ICloudEncryption>(sp => 
            (ICloudEncryption)sp.GetRequiredService<ICloudSyncService>());
        
        // Export and Coordination Services - Transient (per-operation instances)
        services.AddTransient<IExportService, ExportService>();
        services.AddTransient<NoteWindowCoordinator>();
        
        // NoteWindowContext - Scoped (per-window scope)
        services.AddScoped<NoteWindowContext>();
        
        // UI Services - Singleton (shared UI state)
        services.AddSingleton<Dispatcher>(provider => System.Windows.Threading.Dispatcher.CurrentDispatcher);
        services.AddSingleton<TrayMenuService>();
        
        // ViewModels - Singleton (shared application state)
        services.AddSingleton<MainViewModel>();
    }

    private static NoteWindow CreateNoteWindow(IServiceProvider sp, Note note)
    {
        NoteWindow? window = null;
        var mainVm = sp.GetRequiredService<MainViewModel>();
        var context = sp.GetRequiredService<NoteWindowContext>();
        var coordinator = sp.GetRequiredService<NoteWindowCoordinator>();
        
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
        
        window = new NoteWindow(context, coordinator) { DataContext = vm };
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
        _trayMenuService = ServiceProvider.GetRequiredService<TrayMenuService>();
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
            onCreateNewNote: () => InvokeOnUIThread(() => _mainViewModel.CreateNewNote()),
            onShowAll: () => InvokeOnUIThread(() => _mainViewModel.TrayViewModel.ShowAllCommand.Execute(null)),
            onHideAll: () => InvokeOnUIThread(() => _mainViewModel.TrayViewModel.HideAllCommand.Execute(null)),
            onOpenSettings: OpenSettings,
            onExit: () => InvokeOnUIThread(() => _mainViewModel.TrayViewModel.ExitCommand.Execute(null)),
            onSyncNow: TriggerManualSync,
            onOpenNote: noteId => InvokeOnUIThread(() => _mainViewModel.OpenNoteById(noteId))
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
        try
        {
            // Log shutdown start if diagnostics available
            _startupDiagnostics?.StartStep("ApplicationShutdown", "Application", "Shutdown");
            
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
            
            // Clean up notify icon events
            if (_notifyIcon != null)
            {
                _notifyIcon.DoubleClick -= (_, _) => OpenDashboard();
                _notifyIcon.Dispose();
            }
            
            // Dispose crash fix services
            _safeModeController?.Dispose();
            _performanceMonitoringService?.Dispose();
            _startupDiagnostics?.Dispose();
            
            // Dispose all services that implement IDisposable
            try
            {
                (ServiceProvider.GetService<IDebounceService>() as IDisposable)?.Dispose();
                (ServiceProvider.GetService<IThemeService>() as IDisposable)?.Dispose();
                (ServiceProvider.GetService<IHotkeyService>() as IDisposable)?.Dispose();
                (ServiceProvider.GetService<IMonitorService>() as IDisposable)?.Dispose();
                (ServiceProvider.GetService<ICloudSyncService>() as IDisposable)?.Dispose();
                
                // Dispose the service provider itself if it implements IDisposable
                (_serviceProvider as IDisposable)?.Dispose();
            }
            catch
            {
                // Ignore disposal errors during shutdown
            }
        }
        catch
        {
            // Ignore any errors during shutdown
        }
        finally
        {
            base.OnExit(e);
        }
    }

    public static T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Handle critical startup failure with recovery attempts and error dialog
    /// </summary>
    private async Task<bool> HandleCriticalStartupFailure(
        Exception exception, 
        IExceptionLogger? exceptionLogger, 
        IStartupDiagnostics? startupDiagnostics,
        ISafeModeController? safeModeController)
    {
        try
        {
            // Log the critical failure
            if (exceptionLogger != null)
            {
                await exceptionLogger.LogStartupExceptionAsync(exception, new StartupExceptionContext
                {
                    Phase = "CriticalStartupFailure",
                    Component = "Application",
                    Operation = "OnStartup"
                });
                
                // Clean up any partially initialized resources
                exceptionLogger.CleanupTrackedResources();
            }
            else
            {
                // Fallback logging if ExceptionLogger isn't available
                Debug.WriteLine($"Critical startup failure: {exception.Message}");
                Debug.WriteLine($"Stack trace: {exception.StackTrace}");
            }

            // Log diagnostic summary if available
            startupDiagnostics?.LogStartupSummary();

            // Show crash error dialog if crash dialog service is available
            if (_crashErrorDialogService != null)
            {
                var result = _crashErrorDialogService.HandleStartupCrash(exception, "Application Startup");
                return result; // Return true if user wants to restart, false to exit
            }
            
            // Fallback to simple error dialog
            ShowCriticalStartupErrorDialog(exception);
            return false;
        }
        catch (Exception dialogEx)
        {
            // If error handling itself fails, just show basic message and exit
            Debug.WriteLine($"Error handling failed: {dialogEx.Message}");
            ShowCriticalStartupErrorDialog(exception);
            return false;
        }
    }

    /// <summary>
    /// Show critical startup error dialog when startup fails completely
    /// </summary>
    private void ShowCriticalStartupErrorDialog(Exception exception)
    {
        try
        {
            var message = $"DevSticky encountered a critical error during startup and cannot continue.\n\n" +
                         $"Error: {exception.Message}\n\n" +
                         $"Please check the error log for more details or try restarting the application.\n\n" +
                         $"If the problem persists, you may need to reset your configuration files.";

            System.Windows.MessageBox.Show(message, "DevSticky - Critical Startup Error", 
                          MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // If we can't even show a message box, write to debug output
            Debug.WriteLine($"Critical startup error: {exception.Message}");
        }
    }
}
