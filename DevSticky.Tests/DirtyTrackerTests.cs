using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Unit tests for DirtyTracker
/// </summary>
public class DirtyTrackerTests
{
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public void Track_AddsItemToTracker()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item = new TestItem { Id = 1, Name = "Test" };

        // Act
        tracker.Track(item);

        // Assert
        Assert.Equal(1, tracker.Count);
        Assert.Equal(0, tracker.DirtyCount);
    }

    [Fact]
    public void Track_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.Track(null!));
    }

    [Fact]
    public void MarkDirty_MarksItemAsDirty()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item = new TestItem { Id = 1, Name = "Test" };
        tracker.Track(item);

        // Act
        tracker.MarkDirty(item);

        // Assert
        Assert.Equal(1, tracker.DirtyCount);
        Assert.Contains(item, tracker.GetDirtyItems());
    }

    [Fact]
    public void MarkDirty_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.MarkDirty(null!));
    }

    [Fact]
    public void MarkClean_MarksItemAsClean()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item = new TestItem { Id = 1, Name = "Test" };
        tracker.Track(item);
        tracker.MarkDirty(item);

        // Act
        tracker.MarkClean(item);

        // Assert
        Assert.Equal(0, tracker.DirtyCount);
        Assert.DoesNotContain(item, tracker.GetDirtyItems());
    }

    [Fact]
    public void MarkClean_WithNullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => tracker.MarkClean(null!));
    }

    [Fact]
    public void GetDirtyItems_ReturnsOnlyDirtyItems()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item1 = new TestItem { Id = 1, Name = "Test1" };
        var item2 = new TestItem { Id = 2, Name = "Test2" };
        var item3 = new TestItem { Id = 3, Name = "Test3" };
        
        tracker.Track(item1);
        tracker.Track(item2);
        tracker.Track(item3);
        
        tracker.MarkDirty(item1);
        tracker.MarkDirty(item3);

        // Act
        var dirtyItems = tracker.GetDirtyItems().ToList();

        // Assert
        Assert.Equal(2, dirtyItems.Count);
        Assert.Contains(item1, dirtyItems);
        Assert.Contains(item3, dirtyItems);
        Assert.DoesNotContain(item2, dirtyItems);
    }

    [Fact]
    public void Clear_RemovesAllTrackedItems()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item1 = new TestItem { Id = 1, Name = "Test1" };
        var item2 = new TestItem { Id = 2, Name = "Test2" };
        
        tracker.Track(item1);
        tracker.Track(item2);
        tracker.MarkDirty(item1);

        // Act
        tracker.Clear();

        // Assert
        Assert.Equal(0, tracker.Count);
        Assert.Equal(0, tracker.DirtyCount);
        Assert.Empty(tracker.GetDirtyItems());
    }

    [Fact]
    public void Track_SameItemTwice_DoesNotDuplicate()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item = new TestItem { Id = 1, Name = "Test" };

        // Act
        tracker.Track(item);
        tracker.Track(item);

        // Assert
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void MarkDirty_UntrackedItem_AddsAndMarksAsDirty()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var item = new TestItem { Id = 1, Name = "Test" };

        // Act
        tracker.MarkDirty(item);

        // Assert
        Assert.Equal(1, tracker.Count);
        Assert.Equal(1, tracker.DirtyCount);
        Assert.Contains(item, tracker.GetDirtyItems());
    }

    [Fact]
    public void ThreadSafety_ConcurrentOperations_MaintainsConsistency()
    {
        // Arrange
        var tracker = new DirtyTracker<TestItem>();
        var items = Enumerable.Range(0, 100).Select(i => new TestItem { Id = i, Name = $"Test{i}" }).ToList();

        // Act - Perform concurrent operations
        Parallel.ForEach(items, item =>
        {
            tracker.Track(item);
            if (item.Id % 2 == 0)
            {
                tracker.MarkDirty(item);
            }
        });

        // Assert
        Assert.Equal(100, tracker.Count);
        Assert.Equal(50, tracker.DirtyCount);
    }
}
