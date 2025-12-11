using System;
using System.Collections.Generic;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for managing safe mode operations and minimal service configuration
/// </summary>
public interface ISafeModeController : IDisposable
{
    /// <summary>
    /// Gets whether the application is currently running in safe mode
    /// </summary>
    bool IsInSafeMode { get; }
    
    /// <summary>
    /// Gets the safe mode configuration
    /// </summary>
    SafeModeConfig Configuration { get; }
    
    /// <summary>
    /// Activates safe mode with minimal service configuration
    /// </summary>
    /// <param name="reason">Reason for entering safe mode</param>
    void ActivateSafeMode(string reason);
    
    /// <summary>
    /// Deactivates safe mode and attempts normal startup
    /// </summary>
    /// <returns>True if normal startup was successful, false otherwise</returns>
    bool DeactivateSafeMode();
    
    /// <summary>
    /// Detects if safe mode should be activated based on startup failures
    /// </summary>
    /// <param name="startupFailures">List of startup failures</param>
    /// <returns>True if safe mode should be activated</returns>
    bool ShouldActivateSafeMode(IReadOnlyList<ValidationIssue> startupFailures);
    
    /// <summary>
    /// Gets the list of essential services that should be available in safe mode
    /// </summary>
    /// <returns>List of essential service types</returns>
    IReadOnlyList<Type> GetEssentialServices();
    
    /// <summary>
    /// Gets the list of non-essential services that should be disabled in safe mode
    /// </summary>
    /// <returns>List of non-essential service types</returns>
    IReadOnlyList<Type> GetNonEssentialServices();
    
    /// <summary>
    /// Configures minimal services for safe mode operation
    /// </summary>
    /// <param name="serviceProvider">Service provider to configure</param>
    void ConfigureMinimalServices(IServiceProvider serviceProvider);
    
    /// <summary>
    /// Resets configuration to defaults as part of safe mode recovery
    /// </summary>
    void ResetConfigurationToDefaults();
    
    /// <summary>
    /// Gets safe mode status information for display to user
    /// </summary>
    /// <returns>Safe mode status information</returns>
    SafeModeStatus GetSafeModeStatus();
}