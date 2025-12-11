using System;
using System.Collections.Generic;
using System.Linq;

namespace DevSticky.Models;

/// <summary>
/// Result of a validation operation containing success status and any issues found
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed (no critical issues found)
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// List of validation issues found
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();
    
    /// <summary>
    /// Component that was validated
    /// </summary>
    public string Component { get; set; } = string.Empty;
    
    /// <summary>
    /// When the validation was performed
    /// </summary>
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Duration of the validation process
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
    
    /// <summary>
    /// Create a successful validation result
    /// </summary>
    /// <param name="component">Component that was validated</param>
    /// <returns>Successful validation result</returns>
    public static ValidationResult Success(string component = "")
    {
        return new ValidationResult
        {
            IsValid = true,
            Component = component
        };
    }
    
    /// <summary>
    /// Create a failed validation result with a single issue
    /// </summary>
    /// <param name="issue">The validation issue</param>
    /// <param name="component">Component that was validated</param>
    /// <returns>Failed validation result</returns>
    public static ValidationResult Failure(ValidationIssue issue, string component = "")
    {
        return new ValidationResult
        {
            IsValid = false,
            Component = component,
            Issues = new List<ValidationIssue> { issue }
        };
    }
    
    /// <summary>
    /// Create a failed validation result with multiple issues
    /// </summary>
    /// <param name="issues">The validation issues</param>
    /// <param name="component">Component that was validated</param>
    /// <returns>Failed validation result</returns>
    public static ValidationResult Failure(IEnumerable<ValidationIssue> issues, string component = "")
    {
        var issueList = issues.ToList();
        return new ValidationResult
        {
            IsValid = !issueList.Any(i => i.Severity == ValidationSeverity.Critical || i.Severity == ValidationSeverity.Error),
            Component = component,
            Issues = issueList
        };
    }
    
    /// <summary>
    /// Add a validation issue
    /// </summary>
    /// <param name="issue">Issue to add</param>
    public void AddIssue(ValidationIssue issue)
    {
        Issues.Add(issue);
        
        // Update IsValid based on severity
        if (issue.Severity == ValidationSeverity.Critical || issue.Severity == ValidationSeverity.Error)
        {
            IsValid = false;
        }
    }
    
    /// <summary>
    /// Merge another validation result into this one
    /// </summary>
    /// <param name="other">Other validation result to merge</param>
    public void Merge(ValidationResult other)
    {
        Issues.AddRange(other.Issues);
        
        // Update IsValid - if either result is invalid, the merged result is invalid
        IsValid = IsValid && other.IsValid;
        
        // Merge context
        foreach (var kvp in other.Context)
        {
            Context[kvp.Key] = kvp.Value;
        }
    }
    
    /// <summary>
    /// Get issues by severity level
    /// </summary>
    /// <param name="severity">Severity level to filter by</param>
    /// <returns>Issues matching the severity level</returns>
    public IEnumerable<ValidationIssue> GetIssuesBySeverity(ValidationSeverity severity)
    {
        return Issues.Where(i => i.Severity == severity);
    }
    
    /// <summary>
    /// Get critical and error issues
    /// </summary>
    /// <returns>Critical and error issues</returns>
    public IEnumerable<ValidationIssue> GetCriticalIssues()
    {
        return Issues.Where(i => i.Severity == ValidationSeverity.Critical || i.Severity == ValidationSeverity.Error);
    }
    
    /// <summary>
    /// Get warning issues
    /// </summary>
    /// <returns>Warning issues</returns>
    public IEnumerable<ValidationIssue> GetWarnings()
    {
        return Issues.Where(i => i.Severity == ValidationSeverity.Warning);
    }
    
    /// <summary>
    /// Get informational issues
    /// </summary>
    /// <returns>Informational issues</returns>
    public IEnumerable<ValidationIssue> GetInformationalIssues()
    {
        return Issues.Where(i => i.Severity == ValidationSeverity.Information);
    }
    
    /// <summary>
    /// Convert to a formatted string for logging
    /// </summary>
    /// <returns>Formatted validation result</returns>
    public override string ToString()
    {
        var status = IsValid ? "VALID" : "INVALID";
        var issueCount = Issues.Count;
        var criticalCount = GetCriticalIssues().Count();
        var warningCount = GetWarnings().Count();
        
        var result = $"[{ValidationTime:HH:mm:ss.fff}] {Component} Validation - {status}";
        
        if (issueCount > 0)
        {
            result += $" ({criticalCount} critical, {warningCount} warnings, {issueCount} total)";
        }
        
        if (Duration.TotalMilliseconds > 0)
        {
            result += $" ({Duration.TotalMilliseconds:F2}ms)";
        }
        
        return result;
    }
    
    /// <summary>
    /// Get detailed string representation including all issues
    /// </summary>
    /// <returns>Detailed validation result</returns>
    public string ToDetailedString()
    {
        var result = ToString();
        
        if (Issues.Count > 0)
        {
            result += "\n  Issues:";
            foreach (var issue in Issues.OrderByDescending(i => i.Severity))
            {
                result += $"\n    {issue}";
            }
        }
        
        if (Context.Count > 0)
        {
            result += "\n  Context: " + string.Join(", ", Context.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }
        
        return result;
    }
}