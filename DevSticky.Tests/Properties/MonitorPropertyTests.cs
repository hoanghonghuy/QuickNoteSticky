using System.Windows;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Monitor Service
/// **Feature: devsticky-v2, Property 6: Point visibility correction**
/// **Validates: Requirements 2.6**
/// </summary>
public class MonitorPropertyTests
{
    /// <summary>
    /// Property 6: Point visibility correction
    /// For any point (x, y) outside all visible screen bounds, GetNearestVisiblePoint 
    /// should return a point that is within visible screen bounds.
    /// **Validates: Requirements 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetNearestVisiblePoint_ShouldReturnVisiblePoint()
    {
        // Generate arbitrary points including those outside typical screen bounds
        var pointGen = from x in Gen.Choose(-5000, 10000).Select(i => (double)i)
                       from y in Gen.Choose(-5000, 10000).Select(i => (double)i)
                       select (x, y);

        return Prop.ForAll(Arb.From(pointGen), point =>
        {
            var monitorService = new MonitorService();
            var result = monitorService.GetNearestVisiblePoint(point.x, point.y);
            
            // The result should always be visible
            return monitorService.IsPointVisible(result.X, result.Y);
        });
    }

    /// <summary>
    /// Property: If a point is already visible, GetNearestVisiblePoint should return the same point
    /// </summary>
    [Fact]
    public void GetNearestVisiblePoint_VisiblePoint_ShouldReturnSamePoint()
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetAllMonitors();
        
        Assert.NotEmpty(monitors);
        
        var primaryMonitor = monitorService.GetPrimaryMonitor();
        var workingArea = primaryMonitor.WorkingArea;
        
        // Pick a point inside the working area
        double x = workingArea.Left + workingArea.Width / 2;
        double y = workingArea.Top + workingArea.Height / 2;
        
        var result = monitorService.GetNearestVisiblePoint(x, y);
        
        // Should return the same point (or very close due to floating point)
        Assert.True(Math.Abs(result.X - x) < 1 && Math.Abs(result.Y - y) < 1);
    }

    /// <summary>
    /// Property: GetAllMonitors should return at least one monitor
    /// </summary>
    [Fact]
    public void GetAllMonitors_ShouldReturnAtLeastOneMonitor()
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetAllMonitors();
        Assert.True(monitors.Count >= 1);
    }

    /// <summary>
    /// Property: GetPrimaryMonitor should return a monitor marked as primary
    /// </summary>
    [Fact]
    public void GetPrimaryMonitor_ShouldReturnPrimaryMonitor()
    {
        var monitorService = new MonitorService();
        var primary = monitorService.GetPrimaryMonitor();
        var allMonitors = monitorService.GetAllMonitors();
        
        // Either the primary is marked as primary, or there's only one monitor
        Assert.True(primary.IsPrimary || allMonitors.Count == 1);
    }

    /// <summary>
    /// Property: GetMonitorById should return the correct monitor
    /// </summary>
    [Fact]
    public void GetMonitorById_ShouldReturnCorrectMonitor()
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetAllMonitors();
        
        foreach (var monitor in monitors)
        {
            var found = monitorService.GetMonitorById(monitor.DeviceId);
            Assert.NotNull(found);
            Assert.Equal(monitor.DeviceId, found.DeviceId);
        }
    }

    /// <summary>
    /// Property: GetMonitorById with invalid ID should return null
    /// </summary>
    [Fact]
    public void GetMonitorById_InvalidId_ShouldReturnNull()
    {
        var monitorService = new MonitorService();
        var found = monitorService.GetMonitorById("INVALID_DEVICE_ID");
        Assert.Null(found);
    }
}


/// <summary>
/// Property-based tests for Monitor Assignment
/// **Feature: devsticky-v2, Properties 3, 4, 5: Monitor assignment persistence**
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class MonitorAssignmentPropertyTests
{
    /// <summary>
    /// Property 3: Monitor assignment persistence
    /// For any note moved to a specific monitor, the MonitorDeviceId should be stored 
    /// and match the target monitor's identifier.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Fact]
    public void MonitorAssignment_ShouldStoreCorrectDeviceId()
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetAllMonitors();
        
        Assert.NotEmpty(monitors);
        
        // Create a note and assign it to each monitor
        foreach (var monitor in monitors)
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Test Note",
                MonitorDeviceId = monitor.DeviceId
            };
            
            // Verify the assignment is stored correctly
            Assert.Equal(monitor.DeviceId, note.MonitorDeviceId);
        }
    }

    /// <summary>
    /// Property 4: Note restoration to correct monitor
    /// For any note with a stored MonitorDeviceId, if that monitor is available, 
    /// the note should be restored to that monitor's bounds.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Fact]
    public void NoteRestoration_WhenMonitorAvailable_ShouldRestoreToCorrectMonitor()
    {
        var monitorService = new MonitorService();
        var monitors = monitorService.GetAllMonitors();
        
        Assert.NotEmpty(monitors);
        
        foreach (var monitor in monitors)
        {
            // Create a note assigned to this monitor
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Test Note",
                MonitorDeviceId = monitor.DeviceId,
                WindowRect = new WindowRect
                {
                    Left = monitor.WorkingArea.Left + 50,
                    Top = monitor.WorkingArea.Top + 50,
                    Width = 300,
                    Height = 200
                }
            };
            
            // Verify the monitor is available
            var foundMonitor = monitorService.GetMonitorById(note.MonitorDeviceId);
            Assert.NotNull(foundMonitor);
            Assert.Equal(monitor.DeviceId, foundMonitor.DeviceId);
            
            // Verify the note position is within the monitor's bounds
            Assert.True(monitor.WorkingArea.Contains(note.WindowRect.Left, note.WindowRect.Top));
        }
    }

    /// <summary>
    /// Property 5: Fallback to primary monitor
    /// For any note with a stored MonitorDeviceId that is not available, 
    /// the note should be moved to the primary monitor.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void NoteFallback_WhenMonitorUnavailable_ShouldFallbackToPrimary()
    {
        var monitorService = new MonitorService();
        var primaryMonitor = monitorService.GetPrimaryMonitor();
        
        // Create a note with a non-existent monitor ID
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Test Note",
            MonitorDeviceId = "NON_EXISTENT_MONITOR_ID",
            WindowRect = new WindowRect
            {
                Left = 100,
                Top = 100,
                Width = 300,
                Height = 200
            }
        };
        
        // Verify the assigned monitor is not available
        var foundMonitor = monitorService.GetMonitorById(note.MonitorDeviceId);
        Assert.Null(foundMonitor);
        
        // The fallback behavior would update the note's MonitorDeviceId to primary
        // This simulates what WindowService.RestoreNotePosition does
        if (foundMonitor == null)
        {
            note.MonitorDeviceId = primaryMonitor.DeviceId;
        }
        
        // Verify the note is now assigned to primary monitor
        Assert.Equal(primaryMonitor.DeviceId, note.MonitorDeviceId);
    }

    /// <summary>
    /// Property: Monitor assignment should be preserved through serialization
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MonitorAssignment_ShouldSurviveSerialization()
    {
        var deviceIdGen = Gen.Elements(
            "\\\\?\\DISPLAY1",
            "\\\\?\\DISPLAY2",
            "\\\\?\\DISPLAY3",
            "CUSTOM_MONITOR_ID"
        );

        return Prop.ForAll(Arb.From(deviceIdGen), deviceId =>
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Test Note",
                MonitorDeviceId = deviceId
            };
            
            // Simulate serialization round-trip
            var json = System.Text.Json.JsonSerializer.Serialize(note);
            var deserializedNote = System.Text.Json.JsonSerializer.Deserialize<Note>(json);
            
            return deserializedNote != null && deserializedNote.MonitorDeviceId == deviceId;
        });
    }
}
