using System;
using System.Collections.Generic;

namespace DevSticky.Models;

/// <summary>
/// Configuration settings for safe mode operation
/// </summary>
public class SafeModeConfig
{
    /// <summary>
    /// Whether safe mode is enabled
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// Reason for entering safe mode
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when safe mode was activated
    /// </summary>
    public DateTime ActivatedAt { get; set; }
    
    /// <summary>
    /// Whether to use default settings instead of loading user configuration
    /// </summary>
    public bool UseDefaultSettings { get; set; } = true;
    
    /// <summary>
    /// Whether to disable non-essential services
    /// </summary>
    public bool DisableNonEssentialServices { get; set; } = true;
    
    /// <summary>
    /// Whether to disable cloud synchronization in safe mode
    /// </summary>
    public bool DisableCloudSync { get; set; } = true;
    
    /// <summary>
    /// Whether to disable hotkeys in safe mode
    /// </summary>
    public bool DisableHotkeys { get; set; } = true;
    
    /// <summary>
    /// Whether to disable markdown preview in safe mode
    /// </summary>
    public bool DisableMarkdownPreview { get; set; } = true;
    
    /// <summary>
    /// Whether to disable snippets and templates in safe mode
    /// </summary>
    public bool DisableSnippetsAndTemplates { get; set; } = true;
    
    /// <summary>
    /// Whether to disable theme switching in safe mode (use default theme)
    /// </summary>
    public bool DisableThemeSwitching { get; set; } = true;
    
    /// <summary>
    /// Maximum number of notes to load in safe mode (0 = no limit)
    /// </summary>
    public int MaxNotesToLoad { get; set; } = 10;
    
    /// <summary>
    /// Whether to show safe mode indicator in UI
    /// </summary>
    public bool ShowSafeModeIndicator { get; set; } = true;
    
    /// <summary>
    /// List of startup failures that triggered safe mode
    /// </summary>
    public List<string> StartupFailures { get; set; } = new();
    
    /// <summary>
    /// Number of consecutive startup failures before auto-activating safe mode
    /// </summary>
    public int AutoActivateThreshold { get; set; } = 3;
    
    /// <summary>
    /// Creates a default safe mode configuration
    /// </summary>
    /// <returns>Default safe mode configuration</returns>
    public static SafeModeConfig CreateDefault()
    {
        return new SafeModeConfig
        {
            IsEnabled = false,
            UseDefaultSettings = true,
            DisableNonEssentialServices = true,
            DisableCloudSync = true,
            DisableHotkeys = true,
            DisableMarkdownPreview = true,
            DisableSnippetsAndTemplates = true,
            DisableThemeSwitching = true,
            MaxNotesToLoad = 10,
            ShowSafeModeIndicator = true,
            AutoActivateThreshold = 3
        };
    }
    
    /// <summary>
    /// Creates a safe mode configuration for the specified reason
    /// </summary>
    /// <param name="reason">Reason for entering safe mode</param>
    /// <returns>Safe mode configuration</returns>
    public static SafeModeConfig CreateForReason(string reason)
    {
        var config = CreateDefault();
        config.IsEnabled = true;
        config.Reason = reason;
        config.ActivatedAt = DateTime.UtcNow;
        return config;
    }
}

/// <summary>
/// Status information for safe mode
/// </summary>
public class SafeModeStatus
{
    /// <summary>
    /// Whether safe mode is currently active
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Reason for entering safe mode
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// When safe mode was activated
    /// </summary>
    public DateTime? ActivatedAt { get; set; }
    
    /// <summary>
    /// Duration safe mode has been active
    /// </summary>
    public TimeSpan? Duration => ActivatedAt.HasValue ? DateTime.UtcNow - ActivatedAt.Value : null;
    
    /// <summary>
    /// List of disabled services in safe mode
    /// </summary>
    public List<string> DisabledServices { get; set; } = new();
    
    /// <summary>
    /// List of startup failures that triggered safe mode
    /// </summary>
    public List<string> StartupFailures { get; set; } = new();
    
    /// <summary>
    /// Whether configuration was reset to defaults
    /// </summary>
    public bool ConfigurationReset { get; set; }
    
    /// <summary>
    /// Number of notes loaded in safe mode
    /// </summary>
    public int NotesLoaded { get; set; }
    
    /// <summary>
    /// User-friendly description of safe mode status
    /// </summary>
    public string Description => IsActive 
        ? $"Safe mode active: {Reason} (since {ActivatedAt:HH:mm:ss})"
        : "Safe mode inactive";
}