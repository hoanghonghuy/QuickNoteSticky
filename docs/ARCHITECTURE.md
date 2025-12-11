# DevSticky Architecture Documentation

## Overview

DevSticky follows a layered architecture with SOLID principles, dependency injection, and comprehensive testing. This document describes the architectural decisions, patterns, and structure of the refactored codebase.

## Architectural Layers

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│  (Views, ViewModels, Coordinators)      │
├─────────────────────────────────────────┤
│         Application Services            │
│  (Orchestration, Workflows)             │
├─────────────────────────────────────────┤
│           Domain Services               │
│  (Business Logic, Validation)           │
├─────────────────────────────────────────┤
│         Infrastructure Layer            │
│  (Storage, External APIs, Caching)      │
└─────────────────────────────────────────┘
```

### Presentation Layer
- **Views**: WPF UserControls and Windows
- **ViewModels**: MVVM pattern with INotifyPropertyChanged
- **Coordinators**: Orchestrate complex UI workflows (e.g., NoteWindowCoordinator)
- **Handlers**: Specialized UI logic (LinkAutocompleteHandler, MarkdownPreviewHandler)

### Application Services Layer
- **Workflow Orchestration**: Complex business processes
- **Cross-cutting Concerns**: Logging, caching, error handling
- **Service Coordination**: Manages interactions between domain services

### Domain Services Layer
- **Business Logic**: Core application functionality
- **Domain Models**: Note, NoteGroup, NoteTag, etc.
- **Validation**: Business rule enforcement
- **Domain Events**: Decoupled communication

### Infrastructure Layer
- **Data Persistence**: File system, cloud storage
- **External APIs**: OneDrive, Google Drive integration
- **System Services**: File system, dialogs, date/time
- **Caching**: Memory management and performance optimization
- **Crash Handling**: Exception logging, crash analysis, recovery systems
- **Diagnostics**: Startup monitoring, performance tracking, health checks

## SOLID Principles Implementation

### Single Responsibility Principle (SRP)

Each class has a single, well-defined responsibility:

```csharp
// Before: MainViewModel handling everything
public class MainViewModel
{
    // Group management, tag management, note management, UI logic, etc.
}

// After: Separated responsibilities
public class GroupManagementService { /* Only group operations */ }
public class TagManagementService { /* Only tag operations */ }
public class NoteService { /* Only note operations */ }
public class MainViewModel { /* Only UI coordination */ }
```

### Open/Closed Principle (OCP)

System is open for extension, closed for modification:

```csharp
// Cloud provider registry allows new providers without modifying existing code
public interface ICloudProviderRegistry
{
    void RegisterProvider(CloudProvider provider, Func<ICloudStorageProvider> factory);
}

// New providers can be added without changing existing code
registry.RegisterProvider(CloudProvider.Dropbox, () => new DropboxStorageProvider());
```

### Liskov Substitution Principle (LSP)

Derived classes are substitutable for their base classes:

```csharp
// All storage providers can be used interchangeably
ICloudStorageProvider provider = new OneDriveStorageProvider();
provider = new GoogleDriveStorageProvider(); // Seamless substitution
```

### Interface Segregation Principle (ISP)

Interfaces are focused and cohesive:

```csharp
// Before: Fat interface
public interface ICloudSyncService
{
    // Connection, sync, and conflict resolution all mixed together
}

// After: Segregated interfaces
public interface ICloudConnection { /* Only connection concerns */ }
public interface ICloudSync { /* Only sync operations */ }
public interface ICloudConflictResolver { /* Only conflict resolution */ }
```

### Dependency Inversion Principle (DIP)

High-level modules depend on abstractions, not concretions:

```csharp
// High-level service depends on abstraction
public class NoteService
{
    private readonly IStorageService _storage; // Abstraction
    private readonly IErrorHandler _errorHandler; // Abstraction
    
    // Implementation details are injected
}
```

## Crash Handling & Recovery Architecture

### Crash Detection Flow

```
Application Start
        │
        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Startup         │───▶│ Validation      │───▶│ Service         │
│ Diagnostics     │    │ Framework       │    │ Registration    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
        │                       │                       │
        ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Step Tracking   │    │ Issue Detection │    │ Normal Startup  │
│ & Timing        │    │ & Reporting     │    │ Complete        │
└─────────────────┘    └─────────────────┘    └─────────────────┘
        │                       │
        │                       ▼ (Issues Found)
        │               ┌─────────────────┐    ┌─────────────────┐
        │               │ Recovery        │───▶│ Safe Mode       │
        │               │ Manager         │    │ Activation      │
        │               └─────────────────┘    └─────────────────┘
        │                       │                       │
        ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Diagnostic      │    │ Auto Recovery   │    │ Minimal Service │
│ Report          │    │ Actions         │    │ Registration    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Exception Handling Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Exception     │───▶│ Exception       │───▶│ Crash Analytics │
│   Occurs        │    │ Logger          │    │ Service         │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Context         │    │ Dual Logging    │    │ Pattern         │
│ Collection      │    │ (File + Event)  │    │ Analysis        │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Stack Trace     │    │ Windows Event   │    │ Recovery        │
│ Analysis        │    │ Log Entry       │    │ Suggestions     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Recovery System Architecture

```
┌─────────────────┐
│ Validation      │
│ Issues Found    │
└─────────────────┘
         │
         ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Recovery        │───▶│ Risk Assessment │───▶│ User            │
│ Action Analysis │    │ & Prioritization│    │ Confirmation    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Available       │    │ Automatic       │    │ Manual          │
│ Actions         │    │ Recovery        │    │ Recovery        │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ • Config Reset  │    │ • Create Files  │    │ • User Choice   │
│ • File Repair   │    │ • Fix Paths     │    │ • Guided Steps  │
│ • Cache Clear   │    │ • Restart Svc   │    │ • Verification  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Safe Mode Architecture

```
┌─────────────────┐
│ Normal Startup  │
│ Failure         │
└─────────────────┘
         │
         ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Safe Mode       │───▶│ Minimal Service │───▶│ Default         │
│ Controller      │    │ Registration    │    │ Configuration   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Feature         │    │ Essential       │    │ Built-in        │
│ Disabling       │    │ Services Only   │    │ Resources       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Recovery Tools  │    │ Diagnostic      │    │ Safe Mode UI    │
│ Interface       │    │ Information     │    │ Indicators      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Design Patterns

### Repository Pattern
```csharp
public interface IStorageService
{
    Task<IReadOnlyList<Note>> LoadNotesAsync();
    Task SaveNotesAsync(IEnumerable<Note> notes);
    Task<Note?> LoadNoteAsync(Guid noteId);
    Task SaveNoteAsync(Note note);
}
```

### Factory Pattern
```csharp
public static class JsonSerializerOptionsFactory
{
    public static readonly JsonSerializerOptions Default = CreateDefault();
    public static readonly JsonSerializerOptions Compact = CreateCompact();
}
```

### Strategy Pattern
```csharp
public interface ICloudStorageProvider
{
    Task<bool> ConnectAsync();
    Task<SyncResult> SyncAsync(IEnumerable<Note> notes);
}

// Different strategies for different providers
public class OneDriveStorageProvider : ICloudStorageProvider { }
public class GoogleDriveStorageProvider : ICloudStorageProvider { }
```

### Observer Pattern
```csharp
public interface IDirtyTracker<T>
{
    event EventHandler<T> ItemMarkedDirty;
    event EventHandler<T> ItemMarkedClean;
}
```

### Command Pattern
```csharp
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
}
```

## Memory Management Architecture

### LRU Caching System

```csharp
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Application   │───▶│  CacheService   │───▶│   LruCache<T>   │
│     Layer       │    │   (Business)    │    │  (Infrastructure)│
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │ CacheStatistics │
                       │  (Monitoring)   │
                       └─────────────────┘
```

### Dirty Tracking System

```csharp
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  TrackableModel │───▶│  DirtyTracker   │───▶│  SaveQueue      │
│   (Domain)      │    │  (Application)  │    │(Infrastructure) │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
    MarkDirty()            GetDirtyItems()         BatchSave()
```

### Resource Disposal Chain

```csharp
Application Shutdown
        │
        ▼
┌─────────────────┐
│   MainWindow    │
│   Dispose()     │
└─────────────────┘
        │
        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   ViewModels    │───▶│    Services     │───▶│   Resources     │
│   Dispose()     │    │   Dispose()     │    │   Dispose()     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Performance Architecture

### Async Operation Flow

```csharp
UI Thread                 Background Thread              Storage
    │                           │                         │
    ├─ User Action             │                         │
    ├─ Debounce (300ms)        │                         │
    ├─ Queue Operation ────────▶│                         │
    │                          ├─ Process Batch ────────▶│
    │                          │                         ├─ File I/O
    │                          │◀─ Result ───────────────┤
    ◀─ Update UI ──────────────┤                         │
```

### Caching Strategy

```csharp
Request Flow:
┌─────────────┐    Cache Hit    ┌─────────────┐
│   Request   │───────────────▶│   Return    │
│             │                │   Cached    │
└─────────────┘                └─────────────┘
       │
       │ Cache Miss
       ▼
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Load from  │───▶│  Store in   │───▶│   Return    │
│  Storage    │    │   Cache     │    │   Result    │
└─────────────┘    └─────────────┘    └─────────────┘
```

## Error Handling Architecture

### Centralized Error Handling

```csharp
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Operation     │───▶│  ErrorHandler   │───▶│  Error Context  │
│   (Any Layer)   │    │  (Centralized)  │    │  (Metadata)     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │  Recovery       │
                       │  Strategy       │
                       └─────────────────┘
                              │
                              ▼
                       ┌─────────────────┐
                       │  Logging &      │
                       │  Telemetry      │
                       └─────────────────┘
```

### Error Recovery Strategies

1. **Retry with Exponential Backoff**: Network operations, file I/O
2. **Fallback Values**: Configuration loading, cache misses
3. **User Notification**: Validation errors, user-correctable issues
4. **Graceful Degradation**: Feature unavailability, reduced functionality
5. **Circuit Breaker**: Repeated failures, system protection

## Testing Architecture

### Test Pyramid

```
                    ┌─────────────────┐
                    │   E2E Tests     │ ← Few, high-value scenarios
                    │   (Manual)      │
                    └─────────────────┘
                  ┌─────────────────────┐
                  │ Integration Tests   │ ← Service interactions
                  │   (Automated)       │
                  └─────────────────────┘
              ┌─────────────────────────────┐
              │     Unit Tests              │ ← Many, fast, isolated
              │   (Mocked Dependencies)     │
              └─────────────────────────────┘
          ┌─────────────────────────────────────┐
          │      Property-Based Tests           │ ← Correctness properties
          │    (Generated Test Cases)           │
          └─────────────────────────────────────┘
```

### Property-Based Testing

```csharp
// Example: LRU Cache properties
[Property]
public bool LruCache_NeverExceedsMaxSize(NonEmptyArray<int> values)
{
    var cache = new LruCache<int, string>(maxSize: 10);
    
    foreach (var value in values.Get)
    {
        cache.Add(value, value.ToString());
    }
    
    return cache.Count <= 10; // Property: size limit never exceeded
}
```

## Dependency Injection Architecture

### Service Lifetimes

```csharp
Singleton Services (Application Lifetime):
├─ ICacheService (shared cache across application)
├─ IErrorHandler (centralized error handling)
├─ ICloudProviderRegistry (provider registry)
└─ IDebounceService (shared debouncing)

Scoped Services (Per Operation/Window):
├─ IGroupManagementService (per UI context)
├─ ITagManagementService (per UI context)
├─ INoteService (per operation)
└─ ISearchService (per search session)

Transient Services (Per Request):
├─ IDialogService (per dialog)
├─ IFileSystem (per file operation)
└─ IDateTimeProvider (per time request)
```

### Dependency Graph

```csharp
MainViewModel
├─ IGroupManagementService
│  ├─ IStorageService
│  ├─ ICacheService
│  └─ IErrorHandler
├─ ITagManagementService
│  ├─ IStorageService
│  ├─ ICacheService
│  └─ IErrorHandler
└─ INoteService
   ├─ IStorageService
   ├─ IDirtyTracker<Note>
   ├─ ISearchService
   └─ IErrorHandler
```

## Performance Metrics

### Target Metrics (Post-Refactoring)

| Metric | Target | Baseline | Improvement |
|--------|--------|----------|-------------|
| Memory Usage (100 notes) | <50MB | 83MB | 40% reduction |
| Save Performance | <50ms | 200ms | 75% improvement |
| Cache Hit Rate | >90% | N/A | New feature |
| Code Duplication | <5% | 15% | 67% reduction |
| Test Coverage | >80% | 45% | 78% improvement |

### Monitoring Points

```csharp
// Memory monitoring
var memoryUsage = GC.GetTotalMemory(false);
var cacheStats = _cacheService.GetStatistics();

// Performance monitoring
using var activity = Activity.StartActivity("SaveNotes");
var stopwatch = Stopwatch.StartNew();
await SaveNotesAsync();
stopwatch.Stop();

// Cache monitoring
var hitRate = cacheStats.HitRate;
var memoryPressure = cacheStats.CurrentSize / cacheStats.MaxSize;
```

## Security Architecture

### Data Protection

```csharp
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Plain Text    │───▶│   Encryption    │───▶│  Encrypted      │
│   (Memory)      │    │   (AES-256)     │    │  (Storage)      │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         ▲                       │                       │
         │                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Decryption    │◀───│   Key Mgmt      │    │  Cloud Storage  │
│   (On Access)   │    │   (Secure)      │    │  (Encrypted)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Authentication Flow

```csharp
User Request
     │
     ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   OAuth 2.0     │───▶│  Token Storage  │───▶│  API Access     │
│   Flow          │    │  (Encrypted)    │    │  (Authorized)   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Extensibility Points

### Adding New Cloud Providers

```csharp
// 1. Implement interface
public class DropboxStorageProvider : ICloudStorageProvider
{
    // Implementation
}

// 2. Register provider
services.AddTransient<DropboxStorageProvider>();
registry.RegisterProvider(CloudProvider.Dropbox, 
    () => serviceProvider.GetService<DropboxStorageProvider>());
```

### Adding New Services

```csharp
// 1. Define interface
public interface INewService
{
    Task DoSomethingAsync();
}

// 2. Implement service
public class NewService : INewService
{
    private readonly IDependency _dependency;
    
    public NewService(IDependency dependency)
    {
        _dependency = dependency;
    }
}

// 3. Register in DI
services.AddScoped<INewService, NewService>();
```

### Adding New Helper Utilities

```csharp
// 1. Create static helper class
public static class NewHelper
{
    public static string DoSomething(string input)
    {
        // Implementation
    }
}

// 2. Add to Helpers namespace
namespace DevSticky.Helpers
{
    // Helper classes
}

// 3. Document in Helpers/README.md
```

## Migration Path

### Phase-by-Phase Implementation

1. **Foundation**: Core interfaces, DI setup, basic services
2. **SOLID Refactoring**: Extract services, split interfaces
3. **Deduplication**: Shared utilities, common patterns
4. **Performance**: Caching, dirty tracking, optimization
5. **Testing**: Unit tests, property tests, integration tests
6. **Documentation**: API docs, migration guide, architecture

### Backward Compatibility

- Existing data formats maintained
- Configuration files compatible
- User settings preserved
- Gradual migration of components

## Future Considerations

### Scalability
- Plugin architecture for extensions
- Microservices for cloud operations
- Event-driven architecture for loose coupling

### Maintainability
- Automated code quality checks
- Continuous integration/deployment
- Performance regression testing
- Security vulnerability scanning

### Extensibility
- Public API for third-party integrations
- Theme system for UI customization
- Scripting support for automation
- Import/export plugins for different formats

This architecture provides a solid foundation for future growth while maintaining high code quality, performance, and maintainability standards.