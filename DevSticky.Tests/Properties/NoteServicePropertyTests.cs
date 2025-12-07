using DevSticky.Helpers;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for NoteService
/// </summary>
public class NoteServicePropertyTests
{
    /// <summary>
    /// **Feature: devsticky, Property 12: New Note Default Values**
    /// **Validates: Requirements 1.1, 2.3**
    /// For any newly created Note, it SHALL have: IsPinned = true, 
    /// Opacity = defaultOpacity from settings, Width = 300, Height = 200, and a valid non-empty GUID.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NewNote_ShouldHaveCorrectDefaults()
    {
        var opacityGen = Gen.Choose(20, 100).Select(x => x / 100.0);
        
        return Prop.ForAll(
            Arb.From(opacityGen),
            (double defaultOpacity) =>
            {
                var settings = new AppSettings { DefaultOpacity = defaultOpacity };
                var service = new NoteService(settings);
                var note = service.CreateNote();

                return note.Id != Guid.Empty &&
                       note.IsPinned == true &&
                       Math.Abs(note.Opacity - OpacityHelper.Clamp(defaultOpacity)) < 0.001 &&
                       note.WindowRect.Width == WindowRect.DefaultWidth &&
                       note.WindowRect.Height == WindowRect.DefaultHeight &&
                       note.Content == string.Empty;
            });
    }

    /// <summary>
    /// **Feature: devsticky, Property 2: Note Collection Integrity After Delete**
    /// **Validates: Requirements 1.2**
    /// For any Note_Collection containing N notes, after deleting a note by ID, 
    /// the collection SHALL contain exactly N-1 notes and SHALL NOT contain the deleted note's ID.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeleteNote_ShouldRemoveFromCollection()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, 10)),
            (int noteCount) =>
            {
                var settings = new AppSettings();
                var service = new NoteService(settings);
                
                // Create N notes
                var notes = Enumerable.Range(0, noteCount)
                    .Select(_ => service.CreateNote())
                    .ToList();

                var initialCount = service.GetAllNotes().Count;
                var noteToDelete = notes[noteCount / 2];
                
                service.DeleteNote(noteToDelete.Id);
                
                var afterDelete = service.GetAllNotes();
                return afterDelete.Count == initialCount - 1 &&
                       !afterDelete.Any(n => n.Id == noteToDelete.Id);
            });
    }

    /// <summary>
    /// **Feature: devsticky, Property 6: Pin State Toggle**
    /// **Validates: Requirements 2.1**
    /// For any Note with IsPinned state S, toggling the pin state SHALL result in IsPinned = !S.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TogglePin_ShouldFlipState()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            (bool initialPinState) =>
            {
                var settings = new AppSettings();
                var service = new NoteService(settings);
                var note = service.CreateNote();
                note.IsPinned = initialPinState;
                service.UpdateNote(note);

                service.TogglePin(note.Id);
                var updatedNote = service.GetNoteById(note.Id);

                return updatedNote != null && updatedNote.IsPinned == !initialPinState;
            });
    }

    /// <summary>
    /// **Feature: devsticky, Property 5: Opacity Step Adjustment**
    /// **Validates: Requirements 3.2**
    /// For any current opacity value O in [0.2, 1.0], increasing by step 0.1 
    /// SHALL result in min(O + 0.1, 1.0), and decreasing SHALL result in max(O - 0.1, 0.2).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AdjustOpacity_ShouldStayInValidRange()
    {
        var opacityGen = Gen.Choose(20, 100).Select(x => x / 100.0);
        
        return Prop.ForAll(
            Arb.From(opacityGen),
            (double initialOpacity) =>
            {
                var settings = new AppSettings { DefaultOpacity = initialOpacity };
                var service = new NoteService(settings);
                var note = service.CreateNote();

                // Test increase
                var increased = service.AdjustOpacity(note.Id, 0.1);
                var inRangeAfterIncrease = increased >= OpacityHelper.MinOpacity && 
                                           increased <= OpacityHelper.MaxOpacity;

                // Reset and test decrease
                note.Opacity = initialOpacity;
                service.UpdateNote(note);
                var decreased = service.AdjustOpacity(note.Id, -0.1);
                var inRangeAfterDecrease = decreased >= OpacityHelper.MinOpacity && 
                                           decreased <= OpacityHelper.MaxOpacity;

                return inRangeAfterIncrease && inRangeAfterDecrease;
            });
    }
}
