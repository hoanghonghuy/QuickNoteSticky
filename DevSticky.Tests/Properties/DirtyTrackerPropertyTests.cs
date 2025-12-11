using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Dirty Tracking behavior
/// **Feature: code-refactor, Property 2: Dirty Tracking Consistency**
/// **Validates: Requirements 5.3**
/// </summary>
public class DirtyTrackerPropertyTests
{
    /// <summary>
    /// Property 2: Dirty Tracking Consistency
    /// For any item that is modified (marked dirty), it should remain dirty 
    /// until explicitly marked clean.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirtyTracker_WhenItemMarkedDirty_ShouldRemainDirtyUntilMarkedClean()
    {
        return Prop.ForAll(
            ItemsGenerator(),
            items =>
            {
                var tracker = new DirtyTracker<TestItem>();
                
                // Track all items
                foreach (var item in items)
                {
                    tracker.Track(item);
                }
                
                // Mark some items as dirty
                var itemsToMarkDirty = items.Take(items.Count / 2 + 1).ToList();
                foreach (var item in itemsToMarkDirty)
                {
                    tracker.MarkDirty(item);
                }
                
                // Verify all marked items are dirty
                var dirtyItems = tracker.GetDirtyItems().ToList();
                var allMarkedItemsAreDirty = itemsToMarkDirty.All(item => dirtyItems.Contains(item));
                
                if (!allMarkedItemsAreDirty)
                {
                    return false.ToProperty().Label("All marked items should be dirty");
                }
                
                // Verify dirty count matches
                var dirtyCountCorrect = tracker.DirtyCount == itemsToMarkDirty.Count;
                
                if (!dirtyCountCorrect)
                {
                    return false.ToProperty().Label($"Dirty count should be {itemsToMarkDirty.Count}, but was {tracker.DirtyCount}");
                }
                
                // Mark some dirty items as clean
                var itemsToMarkClean = itemsToMarkDirty.Take(itemsToMarkDirty.Count / 2).ToList();
                foreach (var item in itemsToMarkClean)
                {
                    tracker.MarkClean(item);
                }
                
                // Verify only remaining items are dirty
                var remainingDirtyItems = itemsToMarkDirty.Except(itemsToMarkClean).ToList();
                var finalDirtyItems = tracker.GetDirtyItems().ToList();
                
                var remainingItemsStillDirty = remainingDirtyItems.All(item => finalDirtyItems.Contains(item));
                var cleanedItemsNotDirty = itemsToMarkClean.All(item => !finalDirtyItems.Contains(item));
                var finalDirtyCountCorrect = tracker.DirtyCount == remainingDirtyItems.Count;
                
                return (remainingItemsStillDirty && cleanedItemsNotDirty && finalDirtyCountCorrect)
                    .ToProperty()
                    .Label($"Remaining {remainingDirtyItems.Count} items should be dirty, cleaned items should not be dirty");
            });
    }
    
    /// <summary>
    /// Property: Dirty Tracking Idempotence
    /// For any item, marking it dirty multiple times should have the same effect
    /// as marking it dirty once.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirtyTracker_WhenMarkingDirtyMultipleTimes_ShouldBeIdempotent()
    {
        return Prop.ForAll(
            ItemsGenerator(),
            items =>
            {
                var tracker = new DirtyTracker<TestItem>();
                
                // Track all items
                foreach (var item in items)
                {
                    tracker.Track(item);
                }
                
                // Pick an item to mark dirty multiple times
                if (items.Count == 0)
                {
                    return true.ToProperty();
                }
                
                var itemToTest = items[0];
                
                // Mark dirty once
                tracker.MarkDirty(itemToTest);
                var dirtyCountAfterFirst = tracker.DirtyCount;
                var isDirtyAfterFirst = tracker.GetDirtyItems().Contains(itemToTest);
                
                // Mark dirty again
                tracker.MarkDirty(itemToTest);
                var dirtyCountAfterSecond = tracker.DirtyCount;
                var isDirtyAfterSecond = tracker.GetDirtyItems().Contains(itemToTest);
                
                // Mark dirty third time
                tracker.MarkDirty(itemToTest);
                var dirtyCountAfterThird = tracker.DirtyCount;
                var isDirtyAfterThird = tracker.GetDirtyItems().Contains(itemToTest);
                
                return (isDirtyAfterFirst && isDirtyAfterSecond && isDirtyAfterThird &&
                        dirtyCountAfterFirst == dirtyCountAfterSecond &&
                        dirtyCountAfterSecond == dirtyCountAfterThird)
                    .ToProperty()
                    .Label("Multiple MarkDirty calls should be idempotent");
            });
    }
    
    /// <summary>
    /// Property: Clean Tracking Idempotence
    /// For any item, marking it clean multiple times should have the same effect
    /// as marking it clean once.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirtyTracker_WhenMarkingCleanMultipleTimes_ShouldBeIdempotent()
    {
        return Prop.ForAll(
            ItemsGenerator(),
            items =>
            {
                var tracker = new DirtyTracker<TestItem>();
                
                // Track all items and mark them dirty
                foreach (var item in items)
                {
                    tracker.Track(item);
                    tracker.MarkDirty(item);
                }
                
                if (items.Count == 0)
                {
                    return true.ToProperty();
                }
                
                var itemToTest = items[0];
                
                // Mark clean once
                tracker.MarkClean(itemToTest);
                var dirtyCountAfterFirst = tracker.DirtyCount;
                var isDirtyAfterFirst = tracker.GetDirtyItems().Contains(itemToTest);
                
                // Mark clean again
                tracker.MarkClean(itemToTest);
                var dirtyCountAfterSecond = tracker.DirtyCount;
                var isDirtyAfterSecond = tracker.GetDirtyItems().Contains(itemToTest);
                
                // Mark clean third time
                tracker.MarkClean(itemToTest);
                var dirtyCountAfterThird = tracker.DirtyCount;
                var isDirtyAfterThird = tracker.GetDirtyItems().Contains(itemToTest);
                
                return (!isDirtyAfterFirst && !isDirtyAfterSecond && !isDirtyAfterThird &&
                        dirtyCountAfterFirst == dirtyCountAfterSecond &&
                        dirtyCountAfterSecond == dirtyCountAfterThird)
                    .ToProperty()
                    .Label("Multiple MarkClean calls should be idempotent");
            });
    }
    
    /// <summary>
    /// Property: Track Then Dirty Consistency
    /// For any item, tracking it then marking it dirty should result in it being dirty.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirtyTracker_WhenTrackingThenMarkingDirty_ShouldBeDirty()
    {
        return Prop.ForAll(
            ItemsGenerator(),
            items =>
            {
                var tracker = new DirtyTracker<TestItem>();
                
                foreach (var item in items)
                {
                    // Track the item
                    tracker.Track(item);
                    
                    // Verify it's not dirty initially
                    var initiallyClean = !tracker.GetDirtyItems().Contains(item);
                    
                    // Mark it dirty
                    tracker.MarkDirty(item);
                    
                    // Verify it's now dirty
                    var nowDirty = tracker.GetDirtyItems().Contains(item);
                    
                    if (!initiallyClean || !nowDirty)
                    {
                        return false.ToProperty().Label($"Item should be clean initially and dirty after marking");
                    }
                }
                
                // Verify all items are dirty
                var allItemsDirty = items.All(item => tracker.GetDirtyItems().Contains(item));
                var dirtyCountCorrect = tracker.DirtyCount == items.Count;
                
                return (allItemsDirty && dirtyCountCorrect)
                    .ToProperty()
                    .Label("All tracked and marked items should be dirty");
            });
    }
    
    /// <summary>
    /// Property: Clear Removes All Items
    /// For any tracker with items, calling Clear should remove all tracked items.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DirtyTracker_WhenCleared_ShouldRemoveAllItems()
    {
        return Prop.ForAll(
            ItemsGenerator(),
            items =>
            {
                var tracker = new DirtyTracker<TestItem>();
                
                // Track and mark some items dirty
                foreach (var item in items)
                {
                    tracker.Track(item);
                    tracker.MarkDirty(item);
                }
                
                var initialCount = tracker.Count;
                var initialDirtyCount = tracker.DirtyCount;
                
                // Clear the tracker
                tracker.Clear();
                
                // Verify everything is cleared
                var finalCount = tracker.Count;
                var finalDirtyCount = tracker.DirtyCount;
                var noDirtyItems = !tracker.GetDirtyItems().Any();
                
                return (finalCount == 0 && finalDirtyCount == 0 && noDirtyItems)
                    .ToProperty()
                    .Label($"After clear: count should be 0 (was {initialCount}), dirty count should be 0 (was {initialDirtyCount})");
            });
    }

    /// <summary>
    /// Test item class for property testing
    /// </summary>
    private class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        
        public override bool Equals(object? obj)
        {
            return obj is TestItem other && Id == other.Id;
        }
        
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        
        public override string ToString()
        {
            return $"TestItem(Id={Id}, Name={Name})";
        }
    }

    /// <summary>
    /// Generates a list of unique test items for property testing
    /// </summary>
    private static Arbitrary<List<TestItem>> ItemsGenerator()
    {
        var gen = from count in Gen.Choose(0, 10)
                  from ids in Gen.ArrayOf(count, Gen.Choose(1, 1000))
                  from names in Gen.ArrayOf(count, Arb.Generate<NonEmptyString>())
                  let uniqueIds = ids.Distinct().ToArray()
                  where uniqueIds.Length >= count
                  let items = uniqueIds.Take(count)
                      .Zip(names.Take(count), (id, name) => new TestItem { Id = id, Name = name.Get })
                      .ToList()
                  select items;

        return Arb.From(gen);
    }
}