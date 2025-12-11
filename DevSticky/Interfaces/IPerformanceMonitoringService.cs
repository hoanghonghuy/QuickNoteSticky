using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Interface for performance monitoring service
/// </summary>
public interface IPerformanceMonitoringService : IDisposable
{
    /// <summary>
    /// Start timing a category
    /// </summary>
    /// <param name="category">Category name</param>
    void StartCategoryTiming(string category);
    
    /// <summary>
    /// Stop timing a category
    /// </summary>
    /// <param name="category">Category name</param>
    void StopCategoryTiming(string category);
    
    /// <summary>
    /// Mark milestone timestamps
    /// </summary>
    /// <param name="milestone">Milestone name</param>
    void MarkMilestone(string milestone);
    
    /// <summary>
    /// Get comprehensive performance metrics
    /// </summary>
    /// <returns>Performance metrics</returns>
    PerformanceMetrics GetPerformanceMetrics();
    
    /// <summary>
    /// Export performance metrics to file
    /// </summary>
    /// <param name="filePath">File path to export to</param>
    Task ExportPerformanceMetricsAsync(string filePath);
    
    /// <summary>
    /// Log performance summary
    /// </summary>
    void LogPerformanceSummary();
    
    /// <summary>
    /// Get performance warnings
    /// </summary>
    /// <returns>List of performance warnings</returns>
    IReadOnlyList<PerformanceWarning> GetWarnings();
    
    /// <summary>
    /// Clear performance warnings
    /// </summary>
    void ClearWarnings();
    
    /// <summary>
    /// Update performance thresholds
    /// </summary>
    /// <param name="newThresholds">New threshold values</param>
    void UpdateThresholds(PerformanceThresholds newThresholds);
}