using DevSticky.Models;
using DevSticky.Services;
using DevSticky.Interfaces;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Tests for Note dirty tracking functionality
/// </summary>
public class NoteTrackingTests
{
    [Fact]
    public void Note_ShouldInheritFromTrackableModel()
    {
        // Arrange & Act
        var note = new Note();
        
        // Assert
        Assert.IsAssignableFrom<TrackableModel>(note);
        Assert.IsAssignableFrom<ITrackable>(note);
    }

    [Fact]
    public void Note_ShouldStartClean()
    {
        // Arrange & Act
        var note = new Note();
        
        // Assert
        Assert.False(note.IsDirty);
    }

    [Fact]
    public void Note_ShouldBecomesDirtyWhenContentChanges()
    {
        // Arrange
        var note = new Note();
        note.MarkClean(); // Ensure it starts clean
        
        // Act
        note.Content = "New content";
        
        // Assert
        Assert.True(note.IsDirty);
    }

    [Fact]
    public void Note_ShouldBecomesDirtyWhenTitleChanges()
    {
        // Arrange
        var note = new Note();
        note.MarkClean(); // Ensure it starts clean
        
        // Act
        note.Title = "New title";
        
        // Assert
        Assert.True(note.IsDirty);
    }

    [Fact]
    public void Note_ShouldUpdateModifiedDateWhenContentChanges()
    {
        // Arrange
        var note = new Note();
        var originalModifiedDate = note.ModifiedDate;
        
        // Wait a small amount to ensure time difference
        Thread.Sleep(10);
        
        // Act
        note.Content = "New content";
        
        // Assert
        Assert.True(note.ModifiedDate > originalModifiedDate);
    }

    [Fact]
    public void Note_CanBeMarkedClean()
    {
        // Arrange
        var note = new Note();
        note.Content = "Some content"; // Make it dirty
        Assert.True(note.IsDirty);
        
        // Act
        note.MarkClean();
        
        // Assert
        Assert.False(note.IsDirty);
    }

    [Fact]
    public void DirtyTracker_ShouldTrackNotes()
    {
        // Arrange
        var tracker = new DirtyTracker<Note>();
        var note1 = new Note();
        var note2 = new Note();
        
        // Act
        tracker.Track(note1);
        tracker.Track(note2);
        
        // Assert
        Assert.Equal(2, tracker.Count);
        Assert.Equal(0, tracker.DirtyCount);
    }

    [Fact]
    public void DirtyTracker_ShouldIdentifyDirtyNotes()
    {
        // Arrange
        var tracker = new DirtyTracker<Note>();
        var note1 = new Note();
        var note2 = new Note();
        
        tracker.Track(note1);
        tracker.Track(note2);
        
        // Act
        note1.Content = "Modified content"; // This should make note1 dirty
        tracker.MarkDirty(note1); // Explicitly mark as dirty in tracker
        
        // Assert
        Assert.Equal(1, tracker.DirtyCount);
        Assert.Contains(note1, tracker.GetDirtyItems());
        Assert.DoesNotContain(note2, tracker.GetDirtyItems());
    }

    [Fact]
    public void DirtyTracker_ShouldMarkNotesClean()
    {
        // Arrange
        var tracker = new DirtyTracker<Note>();
        var note = new Note();
        
        tracker.Track(note);
        tracker.MarkDirty(note);
        Assert.Equal(1, tracker.DirtyCount);
        
        // Act
        tracker.MarkClean(note);
        
        // Assert
        Assert.Equal(0, tracker.DirtyCount);
        Assert.DoesNotContain(note, tracker.GetDirtyItems());
    }
}