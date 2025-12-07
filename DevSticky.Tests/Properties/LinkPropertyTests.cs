using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using FsCheck;
using FsCheck.Xunit;

namespace DevSticky.Tests.Properties;

/// <summary>
/// Property-based tests for Link operations
/// **Feature: devsticky-v2, Properties 25-28: Link Operations**
/// **Validates: Requirements 7.2, 7.5, 7.7, 7.10**
/// </summary>
public class LinkPropertyTests
{
    /// <summary>
    /// Property 25: Link format generation
    /// For any target note ID and display text, CreateLinkMarkup should produce 
    /// a string in format [[note-id|display-text]].
    /// **Feature: devsticky-v2, Property 25: Link format generation**
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Link_CreateMarkup_ShouldProduceCorrectFormat()
    {
        return Prop.ForAll(LinkMarkupGenerator(), testCase =>
        {
            var noteService = new TestNoteService();
            
            // Add a target note so the service can look up the title
            var targetNote = new Note
            {
                Id = testCase.TargetNoteId,
                Title = testCase.DisplayText ?? "Default Title"
            };
            noteService.AddNote(targetNote);

            var linkService = new LinkService(noteService);
            var markup = linkService.CreateLinkMarkup(testCase.TargetNoteId, testCase.DisplayText);

            // Verify format: [[guid|display-text]]
            var expectedFormat = $"[[{testCase.TargetNoteId}|{testCase.DisplayText ?? targetNote.Title}]]";
            var startsCorrectly = markup.StartsWith("[[");
            var endsCorrectly = markup.EndsWith("]]");
            var containsGuid = markup.Contains(testCase.TargetNoteId.ToString());
            var containsPipe = markup.Contains("|");

            return startsCorrectly && endsCorrectly && containsGuid && containsPipe && markup == expectedFormat;
        });
    }

    /// <summary>
    /// Property 26: Broken link detection
    /// For any note with links to a deleted note, all those links should be 
    /// marked as IsBroken = true.
    /// **Feature: devsticky-v2, Property 26: Broken link detection**
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Link_BrokenDetection_ShouldMarkDeletedTargets()
    {
        return Prop.ForAll(BrokenLinkTestCaseGenerator(), testCase =>
        {
            var noteService = new TestNoteService();
            
            // Add source note with link to non-existent target
            var sourceNote = new Note
            {
                Id = testCase.SourceNoteId,
                Title = "Source Note",
                Content = $"Link to deleted: [[{testCase.DeletedTargetId}|Deleted Note]]"
            };
            noteService.AddNote(sourceNote);

            // Do NOT add the target note - simulating deletion

            var linkService = new LinkService(noteService);
            var links = linkService.GetLinksFromNote(testCase.SourceNoteId);

            // All links to the deleted note should be marked as broken
            var linksToDeleted = links.Where(l => l.TargetNoteId == testCase.DeletedTargetId);
            return linksToDeleted.All(l => l.IsBroken);
        });
    }


    /// <summary>
    /// Property 27: Backlink completeness
    /// For any note A that contains a link to note B, GetBacklinksToNote(B) 
    /// should include a reference to note A.
    /// **Feature: devsticky-v2, Property 27: Backlink completeness**
    /// **Validates: Requirements 7.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Link_Backlinks_ShouldIncludeAllSourceNotes()
    {
        return Prop.ForAll(BacklinkTestCaseGenerator(), testCase =>
        {
            var noteService = new TestNoteService();

            // Add target note
            var targetNote = new Note
            {
                Id = testCase.TargetNoteId,
                Title = "Target Note",
                Content = "This is the target note"
            };
            noteService.AddNote(targetNote);

            // Add source notes that link to target
            foreach (var sourceId in testCase.SourceNoteIds)
            {
                var sourceNote = new Note
                {
                    Id = sourceId,
                    Title = $"Source Note {sourceId}",
                    Content = $"Link to target: [[{testCase.TargetNoteId}|Target Note]]"
                };
                noteService.AddNote(sourceNote);
            }

            var linkService = new LinkService(noteService);
            var backlinks = linkService.GetBacklinksToNote(testCase.TargetNoteId);

            // All source notes should appear in backlinks
            var allSourcesFound = testCase.SourceNoteIds.All(sourceId =>
                backlinks.Any(bl => bl.SourceNoteId == sourceId));

            // Backlink count should match source count
            var countMatches = backlinks.Count == testCase.SourceNoteIds.Count;

            return allSourcesFound && countMatches;
        });
    }

    /// <summary>
    /// Property 28: Link display text update propagation
    /// For any note whose title is changed, all links pointing to that note 
    /// should have their display text updated to the new title.
    /// **Feature: devsticky-v2, Property 28: Link display text update propagation**
    /// **Validates: Requirements 7.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Link_TitleChange_ShouldUpdateDisplayText()
    {
        return Prop.ForAll(TitleChangeTestCaseGenerator(), testCase =>
        {
            var noteService = new TestNoteService();

            // Add target note with old title
            var targetNote = new Note
            {
                Id = testCase.TargetNoteId,
                Title = testCase.NewTitle, // Set to new title (simulating after rename)
                Content = "Target content"
            };
            noteService.AddNote(targetNote);

            // Add source note with link using old title
            var sourceNote = new Note
            {
                Id = testCase.SourceNoteId,
                Title = "Source Note",
                Content = $"Link: [[{testCase.TargetNoteId}|{testCase.OldTitle}]]"
            };
            noteService.AddNote(sourceNote);

            var linkService = new LinkService(noteService);

            // Update links when title changes
            linkService.UpdateLinksOnNoteTitleChangeAsync(
                testCase.TargetNoteId, 
                testCase.OldTitle, 
                testCase.NewTitle).GetAwaiter().GetResult();

            // Verify the source note's content was updated
            var updatedSource = noteService.GetNoteById(testCase.SourceNoteId);
            var expectedMarkup = $"[[{testCase.TargetNoteId}|{testCase.NewTitle}]]";

            return updatedSource != null && updatedSource.Content.Contains(expectedMarkup);
        });
    }

    #region Generators

    private static Arbitrary<LinkMarkupTestCase> LinkMarkupGenerator()
    {
        var gen = from targetId in Gen.Constant(Guid.NewGuid())
                  from displayText in Gen.Elements("My Note", "Important Doc", "Code Reference", "Meeting Notes")
                  select new LinkMarkupTestCase
                  {
                      TargetNoteId = targetId,
                      DisplayText = displayText
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<BrokenLinkTestCase> BrokenLinkTestCaseGenerator()
    {
        var gen = from sourceId in Gen.Constant(Guid.NewGuid())
                  from deletedTargetId in Gen.Constant(Guid.NewGuid())
                  select new BrokenLinkTestCase
                  {
                      SourceNoteId = sourceId,
                      DeletedTargetId = deletedTargetId
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<BacklinkTestCase> BacklinkTestCaseGenerator()
    {
        var gen = from targetId in Gen.Constant(Guid.NewGuid())
                  from sourceCount in Gen.Choose(1, 5)
                  from sourceIds in Gen.ListOf(sourceCount, Gen.Constant(Guid.NewGuid()))
                  select new BacklinkTestCase
                  {
                      TargetNoteId = targetId,
                      SourceNoteIds = sourceIds.Distinct().ToList()
                  };

        return Arb.From(gen);
    }

    private static Arbitrary<TitleChangeTestCase> TitleChangeTestCaseGenerator()
    {
        var gen = from targetId in Gen.Constant(Guid.NewGuid())
                  from sourceId in Gen.Constant(Guid.NewGuid())
                  from oldTitle in Gen.Elements("Old Title", "Previous Name", "Original")
                  from newTitle in Gen.Elements("New Title", "Updated Name", "Renamed")
                  where oldTitle != newTitle
                  select new TitleChangeTestCase
                  {
                      TargetNoteId = targetId,
                      SourceNoteId = sourceId,
                      OldTitle = oldTitle,
                      NewTitle = newTitle
                  };

        return Arb.From(gen);
    }

    #endregion
}


#region Test Case Classes

public class LinkMarkupTestCase
{
    public Guid TargetNoteId { get; set; }
    public string? DisplayText { get; set; }
}

public class BrokenLinkTestCase
{
    public Guid SourceNoteId { get; set; }
    public Guid DeletedTargetId { get; set; }
}

public class BacklinkTestCase
{
    public Guid TargetNoteId { get; set; }
    public List<Guid> SourceNoteIds { get; set; } = new();
}

public class TitleChangeTestCase
{
    public Guid TargetNoteId { get; set; }
    public Guid SourceNoteId { get; set; }
    public string OldTitle { get; set; } = string.Empty;
    public string NewTitle { get; set; } = string.Empty;
}

#endregion

#region Test Helpers

/// <summary>
/// Simple in-memory note service for testing
/// </summary>
public class TestNoteService : INoteService
{
    private readonly List<Note> _notes = new();

    public void AddNote(Note note)
    {
        _notes.Add(note);
    }

    public Note CreateNote()
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = "New Note",
            Content = string.Empty,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };
        _notes.Add(note);
        return note;
    }

    public void DeleteNote(Guid id)
    {
        var note = _notes.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            _notes.Remove(note);
        }
    }

    public void UpdateNote(Note note)
    {
        var index = _notes.FindIndex(n => n.Id == note.Id);
        if (index >= 0)
        {
            note.ModifiedDate = DateTime.UtcNow;
            _notes[index] = note;
        }
    }

    public IReadOnlyList<Note> GetAllNotes() => _notes.AsReadOnly();

    public Note? GetNoteById(Guid id) => _notes.FirstOrDefault(n => n.Id == id);

    public void TogglePin(Guid id)
    {
        var note = GetNoteById(id);
        if (note != null)
        {
            note.IsPinned = !note.IsPinned;
        }
    }

    public double AdjustOpacity(Guid id, double step)
    {
        var note = GetNoteById(id);
        if (note != null)
        {
            note.Opacity = Math.Clamp(note.Opacity + step, 0.1, 1.0);
            return note.Opacity;
        }
        return 0.9;
    }
}

#endregion
