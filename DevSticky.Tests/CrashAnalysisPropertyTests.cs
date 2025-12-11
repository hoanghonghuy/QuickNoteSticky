using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Property-based tests for crash analysis functionality
/// **Feature: crash-fix, Property 1: Comprehensive Crash Analysis**
/// </summary>
public class CrashAnalysisPropertyTests
{
    /// <summary>
    /// **Feature: crash-fix, Property 1: Comprehensive Crash Analysis**
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// 
    /// For any crash scenario, the system should capture complete crash information 
    /// including event log entries, stack traces, and component identification
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CrashAnalysisCompleteness_ShouldCaptureAllRequiredInformation()
    {
        return Prop.ForAll(
            GenerateTestException(),
            GenerateComponentName(),
            (exception, componentName) =>
            {
                // Act: Create crash report from exception
                var crashReport = CrashReport.FromException(exception, componentName);
                
                // Assert: Verify all required information is captured
                var hasTimestamp = crashReport.Timestamp != default(DateTime) && 
                                 crashReport.Timestamp <= DateTime.UtcNow;
                
                var hasExceptionType = !string.IsNullOrEmpty(crashReport.ExceptionType) &&
                                     crashReport.ExceptionType.Contains("Exception");
                
                var hasMessage = !string.IsNullOrEmpty(crashReport.Message);
                
                var hasStackTrace = !string.IsNullOrEmpty(crashReport.StackTrace) &&
                                  crashReport.StackTrace.Contains(exception.GetType().Name);
                
                var hasComponent = !string.IsNullOrEmpty(crashReport.Component) &&
                                 crashReport.Component == componentName;
                
                var hasApplicationVersion = !string.IsNullOrEmpty(crashReport.ApplicationVersion);
                
                var hasOperatingSystem = !string.IsNullOrEmpty(crashReport.OperatingSystem);
                
                var hasRuntimeVersion = !string.IsNullOrEmpty(crashReport.RuntimeVersion);
                
                var hasValidMemoryUsage = crashReport.MemoryUsageMB >= 0;
                
                var hasContext = crashReport.Context != null && crashReport.Context.Count > 0;
                
                return hasTimestamp && hasExceptionType && hasMessage && hasStackTrace && 
                       hasComponent && hasApplicationVersion && hasOperatingSystem && 
                       hasRuntimeVersion && hasValidMemoryUsage && hasContext;
            });
    }
    
    /// <summary>
    /// Property test for crash cause analysis completeness
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CrashCauseAnalysis_ShouldProvideReasonableAnalysis()
    {
        return Prop.ForAll(
            GenerateCrashReport(),
            crashReport =>
            {
                // Act: Analyze crash cause
                var analysis = CrashAnalyzer.AnalyzeCrashCause(crashReport);
                
                // Assert: Analysis should not be empty
                var hasAnalysis = !string.IsNullOrEmpty(analysis);
                
                return hasAnalysis;
            });
    }
    
    /// <summary>
    /// Property test for recovery action suggestions
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecoveryActionSuggestions_ShouldProvideActionableSteps()
    {
        return Prop.ForAll(
            GenerateCrashReport(),
            crashReport =>
            {
                // Act: Get suggested recovery actions
                var actions = CrashAnalyzer.GetSuggestedRecoveryActions(crashReport);
                
                // Assert: Should always provide at least some recovery actions
                var hasActions = actions != null && actions.Count > 0;
                var allActionsAreNonEmpty = actions?.All(action => !string.IsNullOrWhiteSpace(action)) ?? false;
                var hasSafeModeAction = actions?.Any(action => action.Contains("safe mode")) ?? false;
                
                return hasActions && allActionsAreNonEmpty && hasSafeModeAction;
            });
    }
    
    /// <summary>
    /// Property test for event log analysis (when accessible)
    /// </summary>
    [Property(MaxTest = 50)] // Fewer iterations since this involves system resources
    public Property EventLogAnalysis_ShouldHandleAccessGracefully()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 48)), // Hours back to search
            (int hoursBack) =>
            {
                // Act: Attempt to analyze crashes from event log
                var crashes = CrashAnalyzer.AnalyzeCrashesFromEventLog(hoursBack);
                
                // Assert: Should always return a list (even if empty or contains access error)
                var returnsValidList = crashes != null;
                var allCrashesHaveTimestamp = crashes?.All(c => c.Timestamp != default(DateTime)) ?? true;
                var allCrashesHaveComponent = crashes?.All(c => !string.IsNullOrEmpty(c.Component)) ?? true;
                
                return returnsValidList && allCrashesHaveTimestamp && allCrashesHaveComponent;
            });
    }
    
    /// <summary>
    /// Generate test exceptions for property testing
    /// </summary>
    private static Arbitrary<Exception> GenerateTestException()
    {
        var exceptionGenerators = new Gen<Exception>[]
        {
            Gen.Constant<Exception>(new ArgumentNullException("testParam", "Test null reference")),
            Gen.Constant<Exception>(new FileNotFoundException("Test file not found", "test.config")),
            Gen.Constant<Exception>(new DirectoryNotFoundException("Test directory not found")),
            Gen.Constant<Exception>(new UnauthorizedAccessException("Test access denied")),
            Gen.Constant<Exception>(new InvalidOperationException("Test invalid operation")),
            Gen.Constant<Exception>(new System.Text.Json.JsonException("Test JSON parsing error")),
            Gen.Constant<Exception>(new System.IO.IOException("Test IO error")),
            Gen.Constant<Exception>(new OutOfMemoryException("Test memory error")),
            Gen.Constant<Exception>(new System.Configuration.ConfigurationErrorsException("Test configuration error")),
            Gen.Constant<Exception>(new System.Reflection.ReflectionTypeLoadException(new Type[0], new Exception[0], "Test assembly load error"))
        };
        
        return Arb.From(Gen.OneOf<Exception>(exceptionGenerators));
    }
    
    /// <summary>
    /// Generate component names for testing
    /// </summary>
    private static Arbitrary<string> GenerateComponentName()
    {
        var componentNames = new[]
        {
            "App", "Application", "StorageService", "ThemeService", "HotkeyService",
            "CloudSyncService", "NoteService", "WindowService", "TrayMenuService",
            "MarkdownService", "SearchService", "GroupManagementService", "Unknown"
        };
        
        return Arb.From(Gen.Elements(componentNames));
    }
    
    /// <summary>
    /// Generate crash reports for testing
    /// </summary>
    private static Arbitrary<CrashReport> GenerateCrashReport()
    {
        return Arb.From(
            from exception in GenerateTestException().Generator
            from component in GenerateComponentName().Generator
            select CrashReport.FromException(exception, component));
    }
}