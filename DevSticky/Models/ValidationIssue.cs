using System;
using System.Collections.Generic;

namespace DevSticky.Models;

/// <summary>
/// Represents a validation issue found during startup validation
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// Component or area where the issue was found
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of the issue
    /// </summary>
    public string Issue { get; set; } = string.Empty;
    
    /// <summary>
    /// Severity level of the issue
    /// </summary>
    public ValidationSeverity Severity { get; set; }
    
    /// <summary>
    /// Suggested action to resolve the issue
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;
    
    /// <summary>
    /// When the issue was detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Additional context information about the issue
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
    
    /// <summary>
    /// Exception that caused this issue (if applicable)
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Create a critical validation issue
    /// </summary>
    /// <param name="component">Component where issue was found</param>
    /// <param name="issue">Description of the issue</param>
    /// <param name="suggestedAction">Suggested action to resolve</param>
    /// <returns>Critical validation issue</returns>
    public static ValidationIssue Critical(string component, string issue, string suggestedAction = "")
    {
        return new ValidationIssue
        {
            Component = component,
            Issue = issue,
            Severity = ValidationSeverity.Critical,
            SuggestedAction = suggestedAction
        };
    }
    
    /// <summary>
    /// Create an error validation issue
    /// </summary>
    /// <param name="component">Component where issue was found</param>
    /// <param name="issue">Description of the issue</param>
    /// <param name="suggestedAction">Suggested action to resolve</param>
    /// <returns>Error validation issue</returns>
    public static ValidationIssue Error(string component, string issue, string suggestedAction = "")
    {
        return new ValidationIssue
        {
            Component = component,
            Issue = issue,
            Severity = ValidationSeverity.Error,
            SuggestedAction = suggestedAction
        };
    }
    
    /// <summary>
    /// Create a warning validation issue
    /// </summary>
    /// <param name="component">Component where issue was found</param>
    /// <param name="issue">Description of the issue</param>
    /// <param name="suggestedAction">Suggested action to resolve</param>
    /// <returns>Warning validation issue</returns>
    public static ValidationIssue Warning(string component, string issue, string suggestedAction = "")
    {
        return new ValidationIssue
        {
            Component = component,
            Issue = issue,
            Severity = ValidationSeverity.Warning,
            SuggestedAction = suggestedAction
        };
    }
    
    /// <summary>
    /// Create an informational validation issue
    /// </summary>
    /// <param name="component">Component where issue was found</param>
    /// <param name="issue">Description of the issue</param>
    /// <param name="suggestedAction">Suggested action to resolve</param>
    /// <returns>Informational validation issue</returns>
    public static ValidationIssue Information(string component, string issue, string suggestedAction = "")
    {
        return new ValidationIssue
        {
            Component = component,
            Issue = issue,
            Severity = ValidationSeverity.Information,
            SuggestedAction = suggestedAction
        };
    }
    
    /// <summary>
    /// Create a validation issue from an exception
    /// </summary>
    /// <param name="component">Component where exception occurred</param>
    /// <param name="exception">The exception</param>
    /// <param name="severity">Severity level</param>
    /// <param name="suggestedAction">Suggested action to resolve</param>
    /// <returns>Validation issue based on exception</returns>
    public static ValidationIssue FromException(string component, Exception exception, ValidationSeverity severity = ValidationSeverity.Error, string suggestedAction = "")
    {
        return new ValidationIssue
        {
            Component = component,
            Issue = exception.Message,
            Severity = severity,
            SuggestedAction = suggestedAction,
            Exception = exception
        };
    }
    
    /// <summary>
    /// Add context information to this issue
    /// </summary>
    /// <param name="key">Context key</param>
    /// <param name="value">Context value</param>
    public void AddContext(string key, object value)
    {
        Context[key] = value;
    }
    
    /// <summary>
    /// Convert to a formatted string for logging
    /// </summary>
    /// <returns>Formatted validation issue</returns>
    public override string ToString()
    {
        var severityText = Severity switch
        {
            ValidationSeverity.Critical => "CRITICAL",
            ValidationSeverity.Error => "ERROR",
            ValidationSeverity.Warning => "WARNING",
            ValidationSeverity.Information => "INFO",
            _ => "UNKNOWN"
        };
        
        var result = $"[{DetectedAt:HH:mm:ss.fff}] {severityText} - {Component}: {Issue}";
        
        if (!string.IsNullOrEmpty(SuggestedAction))
        {
            result += $" (Suggested: {SuggestedAction})";
        }
        
        return result;
    }
    
    /// <summary>
    /// Get detailed string representation including context and exception
    /// </summary>
    /// <returns>Detailed validation issue</returns>
    public string ToDetailedString()
    {
        var result = ToString();
        
        if (Context.Count > 0)
        {
            result += "\n    Context: " + string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
        
        if (Exception != null)
        {
            result += $"\n    Exception: {Exception.GetType().Name} - {Exception.Message}";
            if (!string.IsNullOrEmpty(Exception.StackTrace))
            {
                result += $"\n    Stack Trace: {Exception.StackTrace}";
            }
        }
        
        return result;
    }
}

/// <summary>
/// Severity levels for validation issues
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message, does not affect validation success
    /// </summary>
    Information = 0,
    
    /// <summary>
    /// Warning message, does not affect validation success but should be noted
    /// </summary>
    Warning = 1,
    
    /// <summary>
    /// Error that causes validation to fail
    /// </summary>
    Error = 2,
    
    /// <summary>
    /// Critical error that causes validation to fail and may prevent startup
    /// </summary>
    Critical = 3
}