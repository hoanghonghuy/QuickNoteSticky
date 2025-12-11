using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DevSticky.Views;

/// <summary>
/// User-friendly error dialog for application crashes with troubleshooting information,
/// safe mode restart options, configuration reset, and diagnostic export functionality.
/// Requirements: 2.3, 6.1, 6.5
/// </summary>
public partial class CrashErrorDialog : Window
{
    private readonly CrashReport _crashReport;
    private readonly ISafeModeController? _safeModeController;
    private readonly IDialogService? _dialogService;
    
    /// <summary>
    /// Result of the dialog interaction
    /// </summary>
    public CrashErrorDialogResult Result { get; private set; } = CrashErrorDialogResult.Close;
    
    /// <summary>
    /// Initializes a new instance of the CrashErrorDialog
    /// </summary>
    /// <param name="crashReport">The crash report containing error details</param>
    /// <param name="safeModeController">Safe mode controller for safe mode operations</param>
    /// <param name="dialogService">Dialog service for additional dialogs</param>
    public CrashErrorDialog(
        CrashReport crashReport, 
        ISafeModeController? safeModeController = null,
        IDialogService? dialogService = null)
    {
        InitializeComponent();
        
        _crashReport = crashReport ?? throw new ArgumentNullException(nameof(crashReport));
        _safeModeController = safeModeController;
        _dialogService = dialogService;
        
        InitializeDialog();
    }
    
    /// <summary>
    /// Initializes the dialog with crash report information
    /// </summary>
    private void InitializeDialog()
    {
        try
        {
            // Set error summary information
            ErrorTypeText.Text = _crashReport.ExceptionType;
            ErrorMessageText.Text = _crashReport.Message;
            ComponentText.Text = $"Component: {_crashReport.Component} | Time: {_crashReport.Timestamp:yyyy-MM-dd HH:mm:ss}";
            
            // Generate diagnostic information
            var diagnosticInfo = GenerateDiagnosticInformation();
            DiagnosticTextBox.Text = diagnosticInfo;
            
            // Enable/disable safe mode button based on controller availability
            SafeModeButton.IsEnabled = _safeModeController != null;
            
            // Set window title with timestamp
            Title = $"DevSticky - Application Error ({_crashReport.Timestamp:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            // Fallback error display if initialization fails
            ErrorTypeText.Text = "CrashErrorDialog Initialization Error";
            ErrorMessageText.Text = $"Failed to initialize error dialog: {ex.Message}";
            ComponentText.Text = "CrashErrorDialog";
            DiagnosticTextBox.Text = $"Dialog initialization failed:\n{ex}";
        }
    }
    
    /// <summary>
    /// Generates comprehensive diagnostic information for export and display
    /// </summary>
    /// <returns>Formatted diagnostic information string</returns>
    private string GenerateDiagnosticInformation()
    {
        var sb = new StringBuilder();
        
        try
        {
            sb.AppendLine("=== DevSticky Crash Report ===");
            sb.AppendLine($"Timestamp: {_crashReport.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"Application Version: {_crashReport.ApplicationVersion}");
            sb.AppendLine($"Operating System: {_crashReport.OperatingSystem}");
            sb.AppendLine($"Runtime Version: {_crashReport.RuntimeVersion}");
            sb.AppendLine($"Memory Usage: {_crashReport.MemoryUsageMB} MB");
            sb.AppendLine();
            
            sb.AppendLine("=== Error Details ===");
            sb.AppendLine($"Exception Type: {_crashReport.ExceptionType}");
            sb.AppendLine($"Component: {_crashReport.Component}");
            sb.AppendLine($"Message: {_crashReport.Message}");
            sb.AppendLine();
            
            sb.AppendLine("=== Stack Trace ===");
            sb.AppendLine(_crashReport.StackTrace);
            sb.AppendLine();
            
            if (_crashReport.Context.Count > 0)
            {
                sb.AppendLine("=== Additional Context ===");
                foreach (var kvp in _crashReport.Context)
                {
                    sb.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }
            
            if (_crashReport.RecoveryActions.Count > 0)
            {
                sb.AppendLine("=== Recovery Actions Attempted ===");
                foreach (var action in _crashReport.RecoveryActions)
                {
                    sb.AppendLine($"- {action}");
                }
                sb.AppendLine();
            }
            
            // Add system information
            sb.AppendLine("=== System Information ===");
            sb.AppendLine($"Machine Name: {Environment.MachineName}");
            sb.AppendLine($"User Name: {Environment.UserName}");
            sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
            sb.AppendLine($"Working Set: {Environment.WorkingSet / (1024 * 1024)} MB");
            sb.AppendLine($"CLR Version: {Environment.Version}");
            sb.AppendLine($"Command Line: {Environment.CommandLine}");
            sb.AppendLine();
            
            // Add safe mode status if available
            if (_safeModeController != null)
            {
                sb.AppendLine("=== Safe Mode Status ===");
                var safeModeStatus = _safeModeController.GetSafeModeStatus();
                sb.AppendLine($"Is Active: {safeModeStatus.IsActive}");
                sb.AppendLine($"Reason: {safeModeStatus.Reason}");
                if (safeModeStatus.ActivatedAt.HasValue)
                {
                    sb.AppendLine($"Activated At: {safeModeStatus.ActivatedAt:yyyy-MM-dd HH:mm:ss}");
                }
                if (safeModeStatus.StartupFailures.Count > 0)
                {
                    sb.AppendLine("Startup Failures:");
                    foreach (var failure in safeModeStatus.StartupFailures)
                    {
                        sb.AppendLine($"  - {failure}");
                    }
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("=== End of Report ===");
        }
        catch (Exception ex)
        {
            sb.Clear();
            sb.AppendLine("=== Diagnostic Generation Error ===");
            sb.AppendLine($"Failed to generate diagnostic information: {ex.Message}");
            sb.AppendLine($"Original crash: {_crashReport.ExceptionType} - {_crashReport.Message}");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Handles safe mode button click
    /// </summary>
    private async void SafeModeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_safeModeController == null)
            {
                await ShowErrorAsync("Safe Mode Unavailable", 
                    "Safe mode controller is not available. Please try restarting the application manually.");
                return;
            }
            
            // Activate safe mode
            _safeModeController.ActivateSafeMode($"User requested after crash: {_crashReport.ExceptionType}");
            
            // Show confirmation
            await ShowInfoAsync("Safe Mode Activated", 
                "Safe mode has been activated. DevSticky will restart with minimal features to help resolve the issue.");
            
            Result = CrashErrorDialogResult.SafeMode;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Safe Mode Error", 
                $"Failed to activate safe mode: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles configuration reset button click
    /// </summary>
    private async void ResetConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Confirm with user
            var confirmed = await ShowConfirmationAsync("Reset Configuration", 
                "This will reset all DevSticky settings to their default values. " +
                "Your notes will not be affected, but you will need to reconfigure your preferences.\n\n" +
                "Do you want to continue?");
            
            if (!confirmed)
                return;
            
            // Reset configuration
            if (_safeModeController != null)
            {
                _safeModeController.ResetConfigurationToDefaults();
            }
            else
            {
                // Fallback configuration reset
                ResetConfigurationFallback();
            }
            
            await ShowInfoAsync("Configuration Reset", 
                "Configuration has been reset to default values. Please restart DevSticky to apply the changes.");
            
            Result = CrashErrorDialogResult.ConfigurationReset;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Configuration Reset Error", 
                $"Failed to reset configuration: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Fallback method to reset configuration when safe mode controller is unavailable
    /// </summary>
    private void ResetConfigurationFallback()
    {
        try
        {
            var appDataPath = PathHelper.GetAppDataPath(AppConstants.AppDataFolderName);
            var settingsPath = Path.Combine(appDataPath, AppConstants.SettingsFileName);
            
            // Create default settings
            var defaultSettings = new AppSettings();
            var settingsJson = JsonSerializer.Serialize(defaultSettings, JsonSerializerOptionsFactory.Default);
            
            // Backup existing settings if they exist
            if (File.Exists(settingsPath))
            {
                var backupPath = Path.Combine(appDataPath, $"settings_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Copy(settingsPath, backupPath, true);
            }
            
            // Write default settings
            File.WriteAllText(settingsPath, settingsJson);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to reset configuration: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Handles diagnostic export button click
    /// </summary>
    private async void ExportDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Diagnostic Information",
                Filter = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"devsticky_crash_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                var diagnosticInfo = GenerateDiagnosticInformation();
                
                if (Path.GetExtension(saveDialog.FileName).ToLowerInvariant() == ".json")
                {
                    // Export as JSON
                    var jsonReport = new
                    {
                        CrashReport = _crashReport,
                        SystemInfo = new
                        {
                            MachineName = Environment.MachineName,
                            UserName = Environment.UserName,
                            ProcessorCount = Environment.ProcessorCount,
                            WorkingSet = Environment.WorkingSet,
                            CLRVersion = Environment.Version.ToString(),
                            CommandLine = Environment.CommandLine
                        },
                        SafeModeStatus = _safeModeController?.GetSafeModeStatus(),
                        ExportedAt = DateTime.UtcNow
                    };
                    
                    var jsonString = JsonSerializer.Serialize(jsonReport, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);
                }
                else
                {
                    // Export as text
                    await File.WriteAllTextAsync(saveDialog.FileName, diagnosticInfo);
                }
                
                await ShowInfoAsync("Export Complete", 
                    $"Diagnostic information has been exported to:\n{saveDialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Error", 
                $"Failed to export diagnostic information: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles GitHub link click
    /// </summary>
    private void GitHubLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/devsticky/devsticky",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Silently fail if unable to open browser
            System.Diagnostics.Debug.WriteLine($"Failed to open GitHub link: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handles try again button click
    /// </summary>
    private void TryAgainButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CrashErrorDialogResult.TryAgain;
        DialogResult = true;
        Close();
    }
    
    /// <summary>
    /// Handles close button click
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = CrashErrorDialogResult.Close;
        DialogResult = false;
        Close();
    }
    
    /// <summary>
    /// Shows an error dialog using the dialog service or fallback
    /// </summary>
    private async Task ShowErrorAsync(string title, string message)
    {
        if (_dialogService != null)
        {
            await _dialogService.ShowErrorAsync(title, message);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Shows an info dialog using the dialog service or fallback
    /// </summary>
    private async Task ShowInfoAsync(string title, string message)
    {
        if (_dialogService != null)
        {
            await _dialogService.ShowInfoAsync(title, message);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    /// <summary>
    /// Shows a confirmation dialog using the dialog service or fallback
    /// </summary>
    private async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        if (_dialogService != null)
        {
            return await _dialogService.ShowConfirmationAsync(title, message);
        }
        else
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
    
    /// <summary>
    /// Shows the crash error dialog with the provided crash report
    /// </summary>
    /// <param name="crashReport">The crash report to display</param>
    /// <param name="owner">Optional owner window</param>
    /// <param name="safeModeController">Optional safe mode controller</param>
    /// <param name="dialogService">Optional dialog service</param>
    /// <returns>The result of the dialog interaction</returns>
    public static CrashErrorDialogResult ShowCrashDialog(
        CrashReport crashReport,
        Window? owner = null,
        ISafeModeController? safeModeController = null,
        IDialogService? dialogService = null)
    {
        try
        {
            var dialog = new CrashErrorDialog(crashReport, safeModeController, dialogService);
            
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else if (Application.Current.MainWindow?.IsVisible == true)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            
            dialog.ShowDialog();
            return dialog.Result;
        }
        catch (Exception ex)
        {
            // Fallback to simple message box if dialog fails
            var message = $"DevSticky encountered an error and needs to close.\n\n" +
                         $"Error: {crashReport.ExceptionType}\n" +
                         $"Message: {crashReport.Message}\n\n" +
                         $"Dialog Error: {ex.Message}";
            
            MessageBox.Show(message, "DevSticky - Application Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            
            return CrashErrorDialogResult.Close;
        }
    }
}

/// <summary>
/// Result of the crash error dialog interaction
/// </summary>
public enum CrashErrorDialogResult
{
    /// <summary>
    /// User chose to close the application
    /// </summary>
    Close,
    
    /// <summary>
    /// User chose to restart in safe mode
    /// </summary>
    SafeMode,
    
    /// <summary>
    /// User chose to reset configuration
    /// </summary>
    ConfigurationReset,
    
    /// <summary>
    /// User chose to try starting the application again
    /// </summary>
    TryAgain
}