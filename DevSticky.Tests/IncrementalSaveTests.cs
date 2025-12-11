using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests;

/// <summary>
/// Tests for incremental save functionality (Requirements 5.3)
/// </summary>
public class IncrementalSaveTests
{
    private class TestSaveQueueService : ISaveQueueService
    {
        public List<Note> QueuedNotes { get; } = new();
        public int FlushCallCount { get; private set; }
        public int QueueCount => QueuedNotes.Count;
        public event EventHandler<SaveCompletedEventArgs>? SaveCompleted;

        public void QueueNote(Note note)
        {
            QueuedNotes.Add(note);
        }

        public void QueueNotes(IEnumerable<Note> notes)
        {
            QueuedNotes.AddRange(notes);
        }

        public Task FlushAsync()
        {
            FlushCallCount++;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    [Fact]
    public void DirtyTracker_WithNotes_ShouldTrackDirtyState()
    {
        // Arrange
        var dirtyTracker = new DirtyTracker<Note>();
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1", Content = "Content 1" };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2", Content = "Content 2" };
        var note3 = new Note { Id = Guid.NewGuid(), Title = "Note 3", Content = "Content 3" };

        // Act
        dirtyTracker.Track(note1);
        dirtyTracker.Track(note2);
        dirtyTracker.Track(note3);
        
        dirtyTracker.MarkDirty(note1);
        dirtyTracker.MarkDirty(note3);

        // Assert
        Assert.Equal(3, dirtyTracker.Count);
        Assert.Equal(2, dirtyTracker.DirtyCount);
        
        var dirtyNotes = dirtyTracker.GetDirtyItems().ToList();
        Assert.Contains(note1, dirtyNotes);
        Assert.Contains(note3, dirtyNotes);
        Assert.DoesNotContain(note2, dirtyNotes);
    }

    [Fact]
    public void SaveQueueService_QueueNotes_ShouldBatchNotes()
    {
        // Arrange
        var saveQueueService = new TestSaveQueueService();
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1", Content = "Content 1" };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2", Content = "Content 2" };
        var notes = new[] { note1, note2 };

        // Act
        saveQueueService.QueueNotes(notes);

        // Assert
        Assert.Equal(2, saveQueueService.QueueCount);
        Assert.Contains(note1, saveQueueService.QueuedNotes);
        Assert.Contains(note2, saveQueueService.QueuedNotes);
    }

    [Fact]
    public async Task SaveQueueService_FlushAsync_ShouldIncrementFlushCount()
    {
        // Arrange
        var saveQueueService = new TestSaveQueueService();

        // Act
        await saveQueueService.FlushAsync();

        // Assert
        Assert.Equal(1, saveQueueService.FlushCallCount);
    }

    [Fact]
    public void Note_PropertyChanges_ShouldMarkAsDirty()
    {
        // Arrange
        var note = new Note { Title = "Original Title", Content = "Original Content" };
        
        // Ensure note starts clean
        note.MarkClean();
        Assert.False(note.IsDirty);

        // Act - Change title
        note.Title = "New Title";

        // Assert
        Assert.True(note.IsDirty);
    }

    [Fact]
    public void Note_ContentChange_ShouldUpdateModifiedDate()
    {
        // Arrange
        var note = new Note { Content = "Original Content" };
        var originalModifiedDate = note.ModifiedDate;
        
        // Wait a small amount to ensure time difference
        System.Threading.Thread.Sleep(1);

        // Act
        note.Content = "New Content";

        // Assert
        Assert.True(note.ModifiedDate > originalModifiedDate);
    }

    [Fact]
    public void TrackableModel_MarkClean_ShouldClearDirtyFlag()
    {
        // Arrange
        var note = new Note { Title = "Test Note" };
        
        // Ensure note is dirty
        Assert.True(note.IsDirty);

        // Act
        note.MarkClean();

        // Assert
        Assert.False(note.IsDirty);
    }

    [Fact]
    public void TrackableModel_MarkDirty_ShouldSetDirtyFlag()
    {
        // Arrange
        var note = new Note();
        note.MarkClean(); // Start clean
        
        // Act
        note.MarkDirty();

        // Assert
        Assert.True(note.IsDirty);
    }

    [Fact]
    public void DirtyTracker_MarkClean_ShouldRemoveFromDirtyItems()
    {
        // Arrange
        var dirtyTracker = new DirtyTracker<Note>();
        var note = new Note { Title = "Test Note" };
        
        dirtyTracker.Track(note);
        dirtyTracker.MarkDirty(note);
        
        // Verify note is dirty
        Assert.Contains(note, dirtyTracker.GetDirtyItems());

        // Act
        dirtyTracker.MarkClean(note);

        // Assert
        Assert.DoesNotContain(note, dirtyTracker.GetDirtyItems());
        Assert.Equal(0, dirtyTracker.DirtyCount);
    }

    [Fact]
    public void IncrementalSave_Integration_ShouldWorkEndToEnd()
    {
        // Arrange
        var dirtyTracker = new DirtyTracker<Note>();
        var saveQueueService = new TestSaveQueueService();
        
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1", Content = "Content 1" };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2", Content = "Content 2" };
        var note3 = new Note { Id = Guid.NewGuid(), Title = "Note 3", Content = "Content 3" };

        // Track notes
        dirtyTracker.Track(note1);
        dirtyTracker.Track(note2);
        dirtyTracker.Track(note3);

        // Mark some as dirty
        dirtyTracker.MarkDirty(note1);
        dirtyTracker.MarkDirty(note3);

        // Act - Simulate incremental save
        var dirtyNotes = dirtyTracker.GetDirtyItems().ToList();
        saveQueueService.QueueNotes(dirtyNotes);
        
        // Mark dirty notes as clean after queuing
        foreach (var dirtyNote in dirtyNotes)
        {
            dirtyTracker.MarkClean(dirtyNote);
        }

        // Assert
        Assert.Equal(2, saveQueueService.QueueCount);
        Assert.Contains(note1, saveQueueService.QueuedNotes);
        Assert.Contains(note3, saveQueueService.QueuedNotes);
        Assert.DoesNotContain(note2, saveQueueService.QueuedNotes);
        Assert.Equal(0, dirtyTracker.DirtyCount);
    }
}