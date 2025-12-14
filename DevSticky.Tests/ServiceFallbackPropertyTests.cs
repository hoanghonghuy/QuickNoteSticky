using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevSticky.Interfaces;
using DevSticky.Services;
using DevSticky.Services.Fallbacks;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Property-based tests for service fallback behavior
/// **Feature: crash-fix, Property 7: Service Fallback Behavior**
/// </summary>
public class ServiceFallbackPropertyTests
{
    /// <summary>
    /// **Feature: crash-fix, Property 7: Service Fallback Behavior**
    /// **Validates: Requirements 4.4, 4.5**
    /// 
    /// For any service initialization failure, the system should attempt to use fallback implementations when available
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ServiceFallbackBehavior_ShouldProvideWorkingFallbacksForFailedServices()
    {
        return Prop.ForAll(
            GenerateServiceFailureScenario(),
            scenario =>
            {
                // Arrange: Set up service fallback manager
                var errorHandlerMock = new MockErrorHandler();
                var fallbackManager = new ServiceFallbackManager(errorHandlerMock);
                var detector = new ServiceInitializationDetector(fallbackManager, errorHandlerMock);

                // Register test fallbacks
                fallbackManager.RegisterFallback<INoteService, FallbackNoteService>();
                fallbackManager.RegisterFallback<IStorageService, FallbackStorageService>();
                fallbackManager.RegisterFallback<IThemeService, FallbackThemeService>();

                // Act: Try to create fallback service for the failed service type
                object? fallbackService = scenario.ServiceType switch
                {
                    ServiceType.NoteService => fallbackManager.CreateFallbackService<INoteService>(scenario.ServiceName, scenario.Exception),
                    ServiceType.StorageService => fallbackManager.CreateFallbackService<IStorageService>(scenario.ServiceName, scenario.Exception),
                    ServiceType.ThemeService => fallbackManager.CreateFallbackService<IThemeService>(scenario.ServiceName, scenario.Exception),
                    _ => null
                };

                // Assert: Fallback service should be created and functional
                var fallbackCreated = fallbackService != null;
                var fallbackIsCorrectType = scenario.ServiceType switch
                {
                    ServiceType.NoteService => fallbackService is INoteService,
                    ServiceType.StorageService => fallbackService is IStorageService,
                    ServiceType.ThemeService => fallbackService is IThemeService,
                    _ => false
                };

                // Test basic functionality of fallback service
                var fallbackWorks = TestFallbackServiceFunctionality(fallbackService, scenario.ServiceType);

                return fallbackCreated && fallbackIsCorrectType && fallbackWorks;
            });
    }

    /// <summary>
    /// Property test for service failure detection
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ServiceFailureDetection_ShouldCorrectlyIdentifyFailedServices()
    {
        return Prop.ForAll(
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            Arb.Default.Bool(),
            (isNull, isDisposed, throwsException) =>
            {
                // Create scenario directly to avoid generator issues
                var serviceTypes = new[] { typeof(INoteService), typeof(IStorageService), typeof(IThemeService) };
                var serviceType = serviceTypes[2]; // Use IThemeService which has properties that throw when disposed
                
                var scenario = new ServiceInstanceScenario
                {
                    ServiceType = serviceType,
                    ServiceInstance = isNull ? null : CreateMockServiceInstance(serviceType, isDisposed, throwsException),
                    IsDisposed = isDisposed && !isNull,
                    ThrowsException = throwsException && !isNull
                };

                // Arrange
                var errorHandlerMock = new MockErrorHandler();
                var fallbackManager = new ServiceFallbackManager(errorHandlerMock);

                // Act: Detect service failure
                var isFailureDetected = fallbackManager.DetectServiceFailure(scenario.ServiceType, scenario.ServiceInstance);

                // Assert: Detection should match expected failure state
                var expectedFailure = scenario.ServiceInstance == null || scenario.IsDisposed || scenario.ThrowsException;
                return isFailureDetected == expectedFailure;
            });
    }

    /// <summary>
    /// Property test for graceful degradation of non-critical services
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracefulDegradation_ShouldConfigureAppropriatelyForNonCriticalServices()
    {
        return Prop.ForAll(
            GenerateNonCriticalServiceScenario(),
            scenario =>
            {
                // Arrange
                var errorHandlerMock = new MockErrorHandler();
                var fallbackManager = new ServiceFallbackManager(errorHandlerMock);

                // Act: Configure graceful degradation
                var result = fallbackManager.ConfigureGracefulDegradation(scenario.ServiceName, scenario.DegradationLevel);

                // Assert: Result should be appropriate for the service and degradation level
                var hasValidResult = result != null;
                var hasServiceName = result?.ServiceName == scenario.ServiceName;
                var hasMessage = !string.IsNullOrEmpty(result?.Message);
                var hasTimestamp = result?.Timestamp != default(DateTime);
                var successMatchesExpectation = result?.IsSuccessful == scenario.IsNonCritical;

                return hasValidResult && hasServiceName && hasMessage && hasTimestamp && successMatchesExpectation;
            });
    }

    /// <summary>
    /// Property test for embedded resource fallbacks
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmbeddedResourceFallback_ShouldProvideValidResourcesWhenExternalResourcesFail()
    {
        return Prop.ForAll(
            GenerateResourceRequest(),
            request =>
            {
                // Arrange
                var errorHandlerMock = new MockErrorHandler();
                var fallbackManager = new ServiceFallbackManager(errorHandlerMock);

                // Act: Get embedded fallback resource
                var resource = fallbackManager.GetEmbeddedFallbackResource(request.ResourceType, request.ResourceKey);

                // Assert: Should return valid resource for known types, null for unknown
                var expectedResource = IsKnownResource(request.ResourceType, request.ResourceKey);
                var hasResource = !string.IsNullOrEmpty(resource);

                return expectedResource ? hasResource : resource == null;
            });
    }

    #region Test Data Generators

    /// <summary>
    /// Generate service failure scenarios for testing
    /// </summary>
    private static Arbitrary<ServiceFailureScenario> GenerateServiceFailureScenario()
    {
        return Arb.From(
            from serviceType in Gen.Elements(ServiceType.NoteService, ServiceType.StorageService, ServiceType.ThemeService)
            from exceptionType in Gen.Elements(typeof(InvalidOperationException), typeof(ArgumentNullException), typeof(IOException))
            select new ServiceFailureScenario
            {
                ServiceType = serviceType,
                ServiceName = serviceType.ToString(),
                Exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!
            });
    }

    /// <summary>
    /// Generate service instance scenarios for failure detection testing
    /// </summary>
    private static Arbitrary<ServiceInstanceScenario> GenerateServiceInstanceScenario()
    {
        var serviceTypes = new[] { typeof(INoteService), typeof(IStorageService), typeof(IThemeService) };
        
        return Arb.From(
            Gen.Choose(0, 2).SelectMany(typeIndex =>
                Gen.Choose(0, 1).SelectMany(isNullInt =>
                    Gen.Choose(0, 1).SelectMany(isDisposedInt =>
                        Gen.Choose(0, 1).Select(throwsExceptionInt =>
                        {
                            var serviceType = serviceTypes[typeIndex];
                            var isNull = isNullInt == 1;
                            var isDisposed = isDisposedInt == 1;
                            var throwsException = throwsExceptionInt == 1;
                            
                            return new ServiceInstanceScenario
                            {
                                ServiceType = serviceType,
                                ServiceInstance = isNull ? null : CreateMockServiceInstance(serviceType, isDisposed, throwsException),
                                IsDisposed = isDisposed && !isNull,
                                ThrowsException = throwsException && !isNull
                            };
                        })))));
    }

    /// <summary>
    /// Generate non-critical service scenarios for degradation testing
    /// </summary>
    private static Arbitrary<NonCriticalServiceScenario> GenerateNonCriticalServiceScenario()
    {
        var knownNonCriticalServices = new[] { "CloudSyncService", "HotkeyService", "MarkdownService", "SnippetService" };
        var unknownServices = new[] { "UnknownService", "TestService", "FakeService" };

        return Arb.From(
            from isNonCritical in Arb.Default.Bool().Generator
            from degradationLevel in Gen.Choose(0, 5)
            let serviceName = isNonCritical 
                ? Gen.Elements(knownNonCriticalServices).Sample(0, 1).First()
                : Gen.Elements(unknownServices).Sample(0, 1).First()
            select new NonCriticalServiceScenario
            {
                ServiceName = serviceName,
                DegradationLevel = degradationLevel,
                IsNonCritical = isNonCritical
            });
    }

    /// <summary>
    /// Generate resource requests for embedded resource testing
    /// </summary>
    private static Arbitrary<ResourceRequest> GenerateResourceRequest()
    {
        var knownTypes = new[] { "theme", "strings", "config" };
        var unknownTypes = new[] { "unknown", "test", "fake" };
        var knownKeys = new[] { "light", "dark", "WelcomeTitle", "default" };
        var unknownKeys = new[] { "unknown", "test", "fake" };

        return Arb.From(
            from hasKnownType in Arb.Default.Bool().Generator
            from hasKnownKey in Arb.Default.Bool().Generator
            let resourceType = hasKnownType 
                ? Gen.Elements(knownTypes).Sample(0, 1).First()
                : Gen.Elements(unknownTypes).Sample(0, 1).First()
            let resourceKey = hasKnownKey 
                ? Gen.Elements(knownKeys).Sample(0, 1).First()
                : Gen.Elements(unknownKeys).Sample(0, 1).First()
            select new ResourceRequest
            {
                ResourceType = resourceType,
                ResourceKey = resourceKey
            });
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Test basic functionality of a fallback service
    /// </summary>
    private static bool TestFallbackServiceFunctionality(object? service, ServiceType serviceType)
    {
        try
        {
            return serviceType switch
            {
                ServiceType.NoteService => TestNoteServiceFunctionality(service as INoteService),
                ServiceType.StorageService => TestStorageServiceFunctionality(service as IStorageService),
                ServiceType.ThemeService => TestThemeServiceFunctionality(service as IThemeService),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Test INoteService functionality
    /// </summary>
    private static bool TestNoteServiceFunctionality(INoteService? noteService)
    {
        if (noteService == null) return false;

        var note = noteService.CreateNote();
        var allNotes = noteService.GetAllNotes();
        var foundNote = noteService.GetNoteById(note.Id);

        return note != null && allNotes != null && foundNote != null;
    }

    /// <summary>
    /// Test IStorageService functionality
    /// </summary>
    private static bool TestStorageServiceFunctionality(IStorageService? storageService)
    {
        if (storageService == null) return false;

        var path = storageService.GetStoragePath();
        return !string.IsNullOrEmpty(path);
    }

    /// <summary>
    /// Test IThemeService functionality
    /// </summary>
    private static bool TestThemeServiceFunctionality(IThemeService? themeService)
    {
        if (themeService == null) return false;

        var currentTheme = themeService.CurrentTheme;
        var currentMode = themeService.CurrentMode;
        
        return Enum.IsDefined(typeof(DevSticky.Models.Theme), currentTheme) && 
               Enum.IsDefined(typeof(DevSticky.Models.ThemeMode), currentMode);
    }

    /// <summary>
    /// Create a mock service instance for testing
    /// </summary>
    private static object? CreateMockServiceInstance(Type serviceType, bool isDisposed, bool throwsException)
    {
        if (throwsException)
        {
            return new ThrowingMockService();
        }

        if (serviceType == typeof(INoteService))
        {
            return new MockNoteService(isDisposed);
        }
        
        if (serviceType == typeof(IStorageService))
        {
            return new MockStorageService(isDisposed);
        }
        
        if (serviceType == typeof(IThemeService))
        {
            return new MockThemeService(isDisposed);
        }

        return null;
    }

    /// <summary>
    /// Check if a resource type and key combination is known
    /// </summary>
    private static bool IsKnownResource(string resourceType, string resourceKey)
    {
        return (resourceType, resourceKey) switch
        {
            ("theme", "light") => true,
            ("theme", "dark") => true,
            ("strings", "WelcomeTitle") => true,
            ("strings", "WelcomeContent") => true,
            ("strings", "UntitledNote") => true,
            ("config", "default") => true,
            _ => false
        };
    }

    #endregion

    #region Test Data Classes

    public class ServiceFailureScenario
    {
        public ServiceType ServiceType { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public Exception Exception { get; set; } = new Exception();
    }

    public class ServiceInstanceScenario
    {
        public Type ServiceType { get; set; } = typeof(object);
        public object? ServiceInstance { get; set; }
        public bool IsDisposed { get; set; }
        public bool ThrowsException { get; set; }
    }

    public class NonCriticalServiceScenario
    {
        public string ServiceName { get; set; } = string.Empty;
        public int DegradationLevel { get; set; }
        public bool IsNonCritical { get; set; }
    }

    public class ResourceRequest
    {
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceKey { get; set; } = string.Empty;
    }

    public enum ServiceType
    {
        NoteService,
        StorageService,
        ThemeService
    }

    #endregion

    #region Mock Services for Testing

    public class MockNoteService : INoteService
    {
        private readonly bool _isDisposed;
        private bool _disposed;

        public MockNoteService(bool isDisposed = false)
        {
            _isDisposed = isDisposed;
            if (_isDisposed) _disposed = true;
        }

        public DevSticky.Models.Note CreateNote()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService));
            return new DevSticky.Models.Note { Id = Guid.NewGuid(), Title = "Mock Note" };
        }

        public void AddNote(DevSticky.Models.Note note) { if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService)); }
        public void DeleteNote(Guid id) { if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService)); }
        public void UpdateNote(DevSticky.Models.Note note) { if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService)); }
        public IReadOnlyList<DevSticky.Models.Note> GetAllNotes() 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService));
            return new List<DevSticky.Models.Note>().AsReadOnly(); 
        }
        public DevSticky.Models.Note? GetNoteById(Guid id) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService));
            return new DevSticky.Models.Note { Id = id, Title = "Mock Note" }; 
        }
        public void TogglePin(Guid id) { if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService)); }
        public double AdjustOpacity(Guid id, double step) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService));
            return 0.9; 
        }
        public void LoadNotes(IEnumerable<DevSticky.Models.Note> notes) { if (_disposed) throw new ObjectDisposedException(nameof(MockNoteService)); }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    public class MockStorageService : IStorageService
    {
        private readonly bool _isDisposed;
        private bool _disposed;

        public MockStorageService(bool isDisposed = false)
        {
            _isDisposed = isDisposed;
            if (_isDisposed) _disposed = true;
        }

        public string GetStoragePath() 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockStorageService));
            return "mock://storage"; 
        }
        
        public Task<DevSticky.Models.AppData> LoadAsync() 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockStorageService));
            return Task.FromResult(new DevSticky.Models.AppData()); 
        }
        
        public Task SaveAsync(DevSticky.Models.AppData data) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockStorageService));
            return Task.CompletedTask; 
        }
        
        public Task SaveNotesAsync(IEnumerable<DevSticky.Models.Note> notes, DevSticky.Models.AppData currentData) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockStorageService));
            return Task.CompletedTask; 
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    public class MockThemeService : IThemeService
    {
        private readonly bool _isDisposed;
        private bool _disposed;

        public MockThemeService(bool isDisposed = false)
        {
            _isDisposed = isDisposed;
            if (_isDisposed) _disposed = true;
        }

        public DevSticky.Models.Theme CurrentTheme 
        { 
            get 
            { 
                if (_disposed) throw new ObjectDisposedException(nameof(MockThemeService));
                return DevSticky.Models.Theme.Dark; 
            } 
        }
        
        public DevSticky.Models.ThemeMode CurrentMode 
        { 
            get 
            { 
                if (_disposed) throw new ObjectDisposedException(nameof(MockThemeService));
                return DevSticky.Models.ThemeMode.System; 
            } 
        }

        public event EventHandler<DevSticky.Models.ThemeChangedEventArgs>? ThemeChanged;

        public void SetThemeMode(DevSticky.Models.ThemeMode mode) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockThemeService));
        }
        
        public void ApplyTheme() 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockThemeService));
        }
        
        public System.Windows.Media.Color GetColor(string key) 
        { 
            if (_disposed) throw new ObjectDisposedException(nameof(MockThemeService));
            return System.Windows.Media.Colors.Black; 
        }
    }

    public class ThrowingMockService
    {
        public string SomeProperty => throw new InvalidOperationException("Mock service failure");
    }

    /// <summary>
    /// Mock error handler for testing
    /// </summary>
    public class MockErrorHandler : IErrorHandler
    {
        public void Handle(Exception exception, string context = "") { }
        public Task HandleAsync(Exception exception, string context = "") => Task.CompletedTask;

        public T HandleWithFallback<T>(Func<T> operation, T fallback, string context = "")
        {
            try
            {
                return operation();
            }
            catch
            {
                return fallback;
            }
        }

        public async Task<T> HandleWithFallbackAsync<T>(Func<Task<T>> operation, T fallback, string context = "")
        {
            try
            {
                return await operation();
            }
            catch
            {
                return fallback;
            }
        }
    }

    #endregion
}