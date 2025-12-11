using System;
using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Views;

namespace DevSticky.Services;

/// <summary>
/// Service for displaying crash error dialogs with proper dependency injection support
/// </summary>
public class CrashErrorDialogService
{
    private readonly ISafeModeController? _safeModeController;
    private readonly IDialogService? _dialogService;
    
    /// <summary>
    /// Initializes a new instance of the CrashErrorDialogService
    /// </summary>
    /// <param name="safeModeController">Safe mode controller for safe mode operations</param>
    /// <param name="dialogService">Dialog service for additional dialogs</param>
    public CrashErrorDialogService(
        ISafeModeController? safeModeController = null,
        IDialogService? dialogService = null)
    {
        _safeModeController = safeModeController;
        _dialogService = dialogService;
    }
    
    /// <summary>
    /// Shows a crash error dialog for the given exception
    /// </summary>
    /// <param name="exception">The exception that caused the crash</param>
    /// <param name="component">The component where the crash occurred</param>
    /// <param name="owner">Optional owner window</param>
    /// <returns>The result of the dialog interaction</returns>
    public CrashErrorDialogResult ShowCrashDialog(
        Exception exception, 
        string component = "Unknown",
        Window? owner = null)
    {
        var crashReport = CrashReport.FromException(exception, component);
        return ShowCrashDialog(crashReport, owner);
    }
    
    /// <summary>
    /// Shows a crash error dialog for the given crash report
    /// </summary>
    /// <param name="crashReport">The crash report to display</param>
    /// <param name="owner">Optional owner window</param>
    /// <returns>The result of the dialog interaction</returns>
    public CrashErrorDialogResult ShowCrashDialog(
        CrashReport crashReport,
        Window? owner = null)
    {
        return CrashErrorDialog.ShowCrashDialog(
            crashReport, 
            owner, 
            _safeModeController, 
            _dialogService);
    }
    
    /// <summary>
    /// Handles a startup crash by showing the appropriate dialog and taking action based on user choice
    /// </summary>
    /// <param name="exception">The startup exception</param>
    /// <param name="component">The component where the crash occurred</param>
    /// <returns>True if the application should attempt to restart, false if it should exit</returns>
    public bool HandleStartupCrash(Exception exception, string component = "Startup")
    {
        var result = ShowCrashDialog(exception, component);
        
        switch (result)
        {
            case CrashErrorDialogResult.SafeMode:
                // Safe mode has been activated by the dialog
                return true;
                
            case CrashErrorDialogResult.ConfigurationReset:
                // Configuration has been reset by the dialog
                return true;
                
            case CrashErrorDialogResult.TryAgain:
                // User wants to try starting normally again
                return true;
                
            case CrashErrorDialogResult.Close:
            default:
                // User chose to close the application
                return false;
        }
    }
}