using System.Text.RegularExpressions;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Services;

/// <summary>
/// Service for managing note links and building the note graph
/// </summary>
public partial class LinkService : ILinkService
{
    // Regex pattern for link syntax: [[note-id|display-text]] or [[note-id]]
    // Captures: Group 1 = note-id (GUID), Group 2 = display-text (optional)
    [GeneratedRegex(@"\[\[([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:\|([^\]]+))?\]\]", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    private readonly INoteService _noteService;

    public LinkService(INoteService noteService)
    {
        _noteService = noteService;
    }

    /// <summary>
    /// Gets all links from a specific note by parsing its content
    /// </summary>
    public IReadOnlyList<NoteLink> GetLinksFromNote(Guid noteId)
    {
        var note = _noteService.GetNoteById(noteId);
        if (note == null)
        {
            return Array.Empty<NoteLink>();
        }

        return ParseLinksFromContent(note.Content, noteId);
    }

    /// <summary>
    /// Gets all backlinks pointing to a specific note by scanning all notes
    /// </summary>
    public IReadOnlyList<NoteLink> GetBacklinksToNote(Guid noteId)
    {
        var backlinks = new List<NoteLink>();
        var allNotes = _noteService.GetAllNotes();

        foreach (var note in allNotes)
        {
            if (note.Id == noteId) continue;

            var links = ParseLinksFromContent(note.Content, note.Id);
            backlinks.AddRange(links.Where(l => l.TargetNoteId == noteId));
        }

        return backlinks.AsReadOnly();
    }


    /// <summary>
    /// Builds the complete graph of all notes and their connections
    /// </summary>
    public Task<NoteGraph> BuildGraphAsync()
    {
        var allNotes = _noteService.GetAllNotes();
        var nodes = new List<NoteGraphNode>();
        var edges = new List<NoteGraphEdge>();
        var backlinkCounts = new Dictionary<Guid, int>();

        // First pass: collect all links and count backlinks
        foreach (var note in allNotes)
        {
            var links = ParseLinksFromContent(note.Content, note.Id);
            
            foreach (var link in links)
            {
                if (!link.IsBroken)
                {
                    edges.Add(new NoteGraphEdge
                    {
                        SourceId = link.SourceNoteId,
                        TargetId = link.TargetNoteId
                    });

                    if (!backlinkCounts.ContainsKey(link.TargetNoteId))
                    {
                        backlinkCounts[link.TargetNoteId] = 0;
                    }
                    backlinkCounts[link.TargetNoteId]++;
                }
            }
        }

        // Second pass: create nodes with link counts
        foreach (var note in allNotes)
        {
            var links = ParseLinksFromContent(note.Content, note.Id);
            var validLinkCount = links.Count(l => !l.IsBroken);
            backlinkCounts.TryGetValue(note.Id, out var backlinkCount);

            nodes.Add(new NoteGraphNode
            {
                NoteId = note.Id,
                Title = note.Title,
                LinkCount = validLinkCount,
                BacklinkCount = backlinkCount
            });
        }

        var graph = new NoteGraph
        {
            Nodes = nodes.AsReadOnly(),
            Edges = edges.AsReadOnly()
        };

        return Task.FromResult(graph);
    }

    /// <summary>
    /// Creates link markup in the format [[note-id|display-text]]
    /// </summary>
    public string CreateLinkMarkup(Guid targetNoteId, string? displayText = null)
    {
        if (string.IsNullOrEmpty(displayText))
        {
            var targetNote = _noteService.GetNoteById(targetNoteId);
            displayText = targetNote?.Title ?? "Unknown Note";
        }

        return $"[[{targetNoteId}|{displayText}]]";
    }

    /// <summary>
    /// Parses link markup and extracts the NoteLink information
    /// </summary>
    public NoteLink? ParseLinkMarkup(string markup)
    {
        if (string.IsNullOrEmpty(markup))
        {
            return null;
        }

        var match = LinkRegex().Match(markup);
        if (!match.Success)
        {
            return null;
        }

        if (!Guid.TryParse(match.Groups[1].Value, out var targetNoteId))
        {
            return null;
        }

        var displayText = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
        var targetNote = _noteService.GetNoteById(targetNoteId);

        return new NoteLink
        {
            SourceNoteId = Guid.Empty, // Unknown without context
            TargetNoteId = targetNoteId,
            DisplayText = displayText,
            StartPosition = match.Index,
            Length = match.Length,
            IsBroken = targetNote == null
        };
    }


    /// <summary>
    /// Parses all links from note content
    /// </summary>
    public IReadOnlyList<NoteLink> ParseLinksFromContent(string content, Guid sourceNoteId)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Array.Empty<NoteLink>();
        }

        var links = new List<NoteLink>();
        var matches = LinkRegex().Matches(content);

        foreach (Match match in matches)
        {
            if (!Guid.TryParse(match.Groups[1].Value, out var targetNoteId))
            {
                continue;
            }

            var displayText = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            var targetNote = _noteService.GetNoteById(targetNoteId);

            links.Add(new NoteLink
            {
                SourceNoteId = sourceNoteId,
                TargetNoteId = targetNoteId,
                DisplayText = displayText,
                StartPosition = match.Index,
                Length = match.Length,
                IsBroken = targetNote == null
            });
        }

        return links.AsReadOnly();
    }

    /// <summary>
    /// Updates display text in all links pointing to a note when its title changes
    /// </summary>
    public Task UpdateLinksOnNoteTitleChangeAsync(Guid noteId, string oldTitle, string newTitle)
    {
        // Create a copy of the list to avoid collection modification during iteration
        var allNotes = _noteService.GetAllNotes().ToList();

        foreach (var note in allNotes)
        {
            if (note.Id == noteId) continue;

            var links = ParseLinksFromContent(note.Content, note.Id);
            var linksToUpdate = links.Where(l => l.TargetNoteId == noteId && l.DisplayText == oldTitle).ToList();

            if (linksToUpdate.Count == 0) continue;

            // Update content by replacing links from end to start to preserve positions
            var updatedContent = note.Content;
            foreach (var link in linksToUpdate.OrderByDescending(l => l.StartPosition))
            {
                var oldMarkup = $"[[{noteId}|{oldTitle}]]";
                var newMarkup = $"[[{noteId}|{newTitle}]]";
                
                // Only replace if the markup at this position matches
                if (link.StartPosition + oldMarkup.Length <= updatedContent.Length)
                {
                    var currentMarkup = updatedContent.Substring(link.StartPosition, link.Length);
                    if (currentMarkup == oldMarkup)
                    {
                        updatedContent = updatedContent.Remove(link.StartPosition, link.Length)
                                                       .Insert(link.StartPosition, newMarkup);
                    }
                }
            }

            if (updatedContent != note.Content)
            {
                note.Content = updatedContent;
                _noteService.UpdateNote(note);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Marks all links to a deleted note as broken
    /// Note: This method doesn't persist the broken state since links are parsed dynamically.
    /// The IsBroken property is determined at parse time based on whether the target note exists.
    /// This method is provided for explicit notification/logging purposes.
    /// </summary>
    public Task MarkBrokenLinksAsync(Guid deletedNoteId)
    {
        // Links are parsed dynamically and IsBroken is determined by checking if target note exists.
        // Since the note is already deleted, any subsequent parsing will automatically mark links as broken.
        // This method can be used for logging or notification purposes.
        
        // Find all notes that have links to the deleted note for potential notification
        var allNotes = _noteService.GetAllNotes();
        var affectedNotes = new List<Note>();

        foreach (var note in allNotes)
        {
            var links = ParseLinksFromContent(note.Content, note.Id);
            if (links.Any(l => l.TargetNoteId == deletedNoteId))
            {
                affectedNotes.Add(note);
            }
        }

        // The broken state will be automatically detected on next parse
        // since the target note no longer exists
        return Task.CompletedTask;
    }
}
