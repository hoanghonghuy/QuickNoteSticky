# DevSticky Helper Utilities

This directory contains common utility classes that eliminate code duplication and provide reusable functionality across the application.

## Available Helpers

### 1. JsonSerializerOptionsFactory
Shared JSON serialization configuration to eliminate duplication.

**Key Properties:**
- `Default` - Standard options with indentation and camelCase
- `Compact` - Minified options without indentation

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Use shared configuration
var json = JsonSerializer.Serialize(data, JsonSerializerOptionsFactory.Default);
var compactJson = JsonSerializer.Serialize(data, JsonSerializerOptionsFactory.Compact);
```

### 2. MonitorBoundsHelper
Multi-monitor window positioning utilities.

**Key Methods:**
- `EnsureWindowInBounds(Window, MonitorInfo)` - Keep window within monitor bounds
- `CenterWindowOnMonitor(Window, MonitorInfo)` - Center window on specific monitor
- `CalculateRelativePosition(Window, MonitorInfo)` - Get relative position (0-1)
- `ApplyRelativePosition(Window, MonitorInfo, double, double)` - Apply relative position

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Center window on primary monitor
MonitorBoundsHelper.CenterWindowOnMonitor(window, primaryMonitor);

// Ensure window stays in bounds
MonitorBoundsHelper.EnsureWindowInBounds(window, currentMonitor);

// Save/restore relative position
var (relX, relY) = MonitorBoundsHelper.CalculateRelativePosition(window, monitor);
// Later...
MonitorBoundsHelper.ApplyRelativePosition(window, monitor, relX, relY);
```

### 3. OpacityHelper
Window opacity calculations and validation.

**Key Methods:**
- `ValidateOpacity(double)` - Ensure opacity is in valid range (0.2-1.0)
- `ToPercentage(double)` - Convert opacity to percentage string
- `FromPercentage(string)` - Parse percentage string to opacity
- `GetOpacitySteps()` - Get predefined opacity levels

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Validate and set opacity
var validOpacity = OpacityHelper.ValidateOpacity(userInput);
window.Opacity = validOpacity;

// Display as percentage
var display = OpacityHelper.ToPercentage(window.Opacity); // "80%"
```

### 4. WeakEventHelper
Memory-safe event handling to prevent memory leaks.

**Key Methods:**
- `Subscribe<T>(INotifyPropertyChanged, string, Action<T>)` - Weak property change subscription
- `Unsubscribe(INotifyPropertyChanged, string)` - Remove weak subscription
- `CreateWeakEventHandler<T>(Action<T>)` - Create weak event handler

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Subscribe without creating strong reference
WeakEventHelper.Subscribe<string>(viewModel, nameof(ViewModel.Title), OnTitleChanged);

// Automatic cleanup when subscriber is garbage collected
```

### 5. WpfResourceHelper
WPF resource cleanup utilities.

**Key Methods:**
- `DisposeResources(FrameworkElement)` - Dispose WPF element resources
- `ClearBindings(FrameworkElement)` - Clear data bindings
- `DetachEvents(FrameworkElement)` - Detach event handlers
- `CleanupAvalonEdit(TextEditor)` - Specific cleanup for AvalonEdit

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Cleanup when closing window
protected override void OnClosed(EventArgs e)
{
    WpfResourceHelper.DisposeResources(this);
    WpfResourceHelper.CleanupAvalonEdit(textEditor);
    base.OnClosed(e);
}
```

### 6. MemoryLeakDetector
Development utility for detecting memory leaks.

**Key Methods:**
- `StartTracking(string)` - Begin tracking memory usage
- `StopTracking(string)` - End tracking and report usage
- `ForceGarbageCollection()` - Force GC for accurate measurements
- `GetMemoryUsage()` - Get current memory usage

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Track memory usage during operation
MemoryLeakDetector.StartTracking("LoadNotes");
await LoadNotesAsync();
MemoryLeakDetector.StopTracking("LoadNotes");
```

### 7. PerformanceBenchmark
Performance measurement utilities.

**Key Methods:**
- `Measure(Action, string)` - Measure action execution time
- `MeasureAsync(Func<Task>, string)` - Measure async operation time
- `StartTimer(string)` - Start named timer
- `StopTimer(string)` - Stop timer and get elapsed time

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Measure operation performance
var elapsed = PerformanceBenchmark.Measure(() => SaveAllNotes(), "SaveAllNotes");

// Async measurement
var asyncElapsed = await PerformanceBenchmark.MeasureAsync(
    () => SyncToCloudAsync(), 
    "CloudSync");
```

### 8. StringHelper
Common string operations and validation.

**Key Methods:**
- `IsNullOrWhiteSpace(string?)` - Check if string is null, empty, or whitespace
- `IsNullOrEmpty(string?)` - Check if string is null or empty
- `Truncate(string?, int, bool)` - Truncate string to max length with optional ellipsis
- `EnsureNotNull(string?)` - Return empty string if null
- `NormalizeLineEndings(string?, string?)` - Normalize line endings
- `RemoveWhitespace(string?)` - Remove all whitespace
- `Capitalize(string?)` - Capitalize first letter
- `ToTitleCase(string?)` - Convert to Title Case
- `CountOccurrences(string?, string, StringComparison)` - Count substring occurrences

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Truncate a long name
var displayName = StringHelper.Truncate(fullName, 30, addEllipsis: true);

// Validate input
if (StringHelper.IsNullOrWhiteSpace(userInput))
{
    return;
}

// Format text
var title = StringHelper.ToTitleCase(rawTitle);
```

### 2. GuidHelper
GUID operations and validation.

**Key Methods:**
- `NewGuid()` - Generate new GUID
- `IsEmpty(Guid)` - Check if GUID is empty
- `IsNotEmpty(Guid)` - Check if GUID is not empty
- `TryParse(string?, out Guid)` - Try parse string as GUID
- `ParseOrEmpty(string?)` - Parse or return Guid.Empty
- `IsValidGuid(string?)` - Validate GUID format
- `ToShortString(Guid)` - Get first 8 characters for display
- `ToUpperString(Guid)` - Convert to uppercase
- `ToLowerString(Guid)` - Convert to lowercase

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Generate new ID
var noteId = GuidHelper.NewGuid();

// Validate GUID
if (GuidHelper.IsValidGuid(userInput))
{
    var id = GuidHelper.ParseOrEmpty(userInput);
}

// Display short ID for logging
var shortId = GuidHelper.ToShortString(noteId);
Console.WriteLine($"Processing note {shortId}...");
```

### 3. CollectionHelper
Common collection operations.

**Key Methods:**
- `IsNullOrEmpty<T>(IEnumerable<T>?)` - Check if collection is null or empty
- `HasElements<T>(IEnumerable<T>?)` - Check if collection has elements
- `EmptyIfNull<T>(IEnumerable<T>?)` - Return empty collection if null
- `GetOrDefault<T>(IList<T>?, int, T)` - Safely get element at index
- `AddIfNotExists<T>(ICollection<T>, T)` - Add only if not present
- `RemoveWhere<T>(ICollection<T>, Func<T, bool>)` - Remove matching items
- `Batch<T>(IEnumerable<T>, int)` - Partition into batches
- `ToDictionarySafe<TSource, TKey, TValue>()` - Create dictionary handling duplicates
- `Shuffle<T>(IList<T>)` - Shuffle list in place

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Safe collection access
if (CollectionHelper.HasElements(notes))
{
    var firstNote = CollectionHelper.GetOrDefault(notes, 0);
}

// Add unique items
CollectionHelper.AddIfNotExists(tags, newTag);

// Process in batches
foreach (var batch in CollectionHelper.Batch(largeList, 100))
{
    ProcessBatch(batch);
}
```

### 4. DateTimeHelper
DateTime operations and formatting.

**Key Methods:**
- `ToStandardDate(DateTime)` - Format as yyyy-MM-dd
- `ToStandardDateTime(DateTime)` - Format as yyyy-MM-dd HH:mm:ss
- `ToShortDateTime(DateTime)` - Format as yyyy-MM-dd HH:mm
- `ToFilenameDateTime(DateTime)` - Format as yyyyMMdd_HHmmss
- `ToIso8601(DateTime)` - Format as ISO 8601
- `ToRelativeTime(DateTime, DateTime?)` - Get relative time string ("2 hours ago")
- `IsToday(DateTime)` - Check if date is today
- `IsYesterday(DateTime)` - Check if date is yesterday
- `IsThisWeek(DateTime)` - Check if date is this week
- `StartOfDay(DateTime)` - Get start of day (00:00:00)
- `EndOfDay(DateTime)` - Get end of day (23:59:59.999)
- `CalculateAge(DateTime, DateTime?)` - Calculate age in years
- `TryParseFlexible(string?, out DateTime)` - Parse using multiple formats

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Format for display
var displayDate = DateTimeHelper.ToStandardDate(note.CreatedDate);
var relativeTime = DateTimeHelper.ToRelativeTime(note.ModifiedDate);

// Format for filename
var backupName = $"backup_{DateTimeHelper.ToFilenameDateTime(DateTime.Now)}.json";

// Date comparisons
if (DateTimeHelper.IsToday(note.CreatedDate))
{
    // Handle today's notes
}
```

### 5. ValidationHelper
Common validation operations.

**Key Methods:**
- `IsValidEmail(string?)` - Validate email format
- `IsValidUrl(string?)` - Validate URL format
- `IsValidHexColor(string?)` - Validate hex color code
- `IsInRange<T>(T, T, T)` - Check if value in range
- `IsLengthInRange(string?, int, int)` - Check string length
- `IsCountInRange<T>(IEnumerable<T>?, int, int)` - Check collection count
- `IsAlphanumeric(string?)` - Check if only letters and digits
- `IsAlpha(string?)` - Check if only letters
- `IsNumeric(string?)` - Check if only digits
- `IsValidFilePath(string?)` - Validate file path format
- `IsValidDirectoryPath(string?)` - Validate directory path format
- `IsNotNull<T>(T?)` - Check if not null
- `HasValue<T>(T?)` - Check if nullable has value
- `All(params bool[])` - All conditions true
- `Any(params bool[])` - Any condition true

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Validate user input
if (!ValidationHelper.IsValidEmail(emailInput))
{
    ShowError("Invalid email address");
    return;
}

// Validate color
if (ValidationHelper.IsValidHexColor(colorInput))
{
    tag.Color = colorInput;
}

// Validate range
if (ValidationHelper.IsInRange(opacity, 0.2, 1.0))
{
    note.Opacity = opacity;
}

// Multiple validations
if (ValidationHelper.All(
    ValidationHelper.IsLengthInRange(name, 1, 50),
    ValidationHelper.IsAlphanumeric(name)))
{
    // Name is valid
}
```

### 6. PathHelper
File path operations.

**Key Methods:**
- `Combine(params string[])` - Combine path segments
- `GetAppDataPath(string)` - Get application data folder
- `EnsureDirectoryExists(string)` - Create directory if needed
- `GetDirectoryName(string?)` - Get directory from path
- `GetFileName(string?)` - Get filename from path
- `GetFileNameWithoutExtension(string?)` - Get filename without extension
- `GetExtension(string?)` - Get file extension
- `ChangeExtension(string, string)` - Change file extension
- `GetUniqueFilePath(string)` - Generate unique path with counter
- `SanitizeFileName(string, char)` - Remove invalid characters
- `IsValidPath(string?)` - Validate path format
- `GetRelativePath(string, string)` - Get relative path
- `NormalizePath(string?)` - Normalize path separators

**Usage Example:**
```csharp
using DevSticky.Helpers;

// Build paths safely
var storagePath = PathHelper.Combine(
    PathHelper.GetAppDataPath("DevSticky"),
    "notes.json");

// Ensure directory exists
PathHelper.EnsureDirectoryExists(PathHelper.GetDirectoryName(storagePath));

// Generate unique filename
var backupPath = PathHelper.GetUniqueFilePath(originalPath);

// Sanitize user input
var safeFileName = PathHelper.SanitizeFileName(userInput);
```

## New Architecture Helpers

The refactored architecture includes several new helper categories:

### Memory Management Helpers
- **LruCache**: Generic LRU cache implementation with automatic eviction
- **MemoryLeakDetector**: Development utility for tracking memory usage
- **WeakEventHelper**: Prevents memory leaks from event subscriptions

### Performance Helpers
- **PerformanceBenchmark**: Measures execution time for operations
- **BenchmarkRunner**: Comprehensive benchmarking with statistical analysis
- **SimpleBenchmarkDemo**: Example usage of benchmarking utilities

### Code Quality Helpers
- **JsonSerializerOptionsFactory**: Eliminates JSON configuration duplication
- **MonitorBoundsHelper**: Multi-monitor window positioning
- **WpfResourceHelper**: Proper WPF resource cleanup

## Integration with Existing Code

The helper utilities have been integrated into several services:

### StorageService
- Uses `PathHelper.Combine()` for path construction
- Uses `PathHelper.GetAppDataPath()` for application data folder
- Uses `PathHelper.EnsureDirectoryExists()` for directory creation
- Uses `StringHelper.IsNullOrEmpty()` for validation

### TemplateService
- Uses `PathHelper` for path operations
- Uses `StringHelper.ToTitleCase()` for formatting display names
- Uses `StringHelper.IsNullOrEmpty()` for validation

### SearchService
- Uses `StringHelper.IsNullOrEmpty()` for input validation

### GroupManagementService
- Uses `StringHelper.Truncate()` for name length limiting

### TagManagementService
- Uses `StringHelper.Truncate()` for name length limiting

## Benefits

1. **Code Deduplication**: Eliminates repeated utility code across the application
2. **Consistency**: Provides consistent behavior for common operations
3. **Maintainability**: Centralized location for utility logic makes updates easier
4. **Testability**: Utility methods can be easily unit tested
5. **Readability**: Clear, descriptive method names improve code readability
6. **Reusability**: Utilities can be used across all layers of the application

## Requirements Satisfied

This implementation satisfies **Requirement 2.4** from the code refactoring specification:
- Reviewed codebase for duplicate utility methods
- Created utility classes for common operations
- Updated code to use utilities
- Reduced code duplication and improved maintainability
