using DevSticky.Models;

namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing note links and building the note graph
/// </summary>
public interface ILinkService
{
    /// <summary>
    /// Gets all links from a specific note
    /// </summary>
    /// <param name="noteId">The source note ID</param>
    /// <returns>List of links originating from the note</returns>
    IReadOnlyList<NoteLink> GetLinksFromNote(Guid noteId);

    /// <summary>
    /// Gets all backlinks pointing to a specific note
    /// </summary>
    /// <param name="noteId">The target note ID</param>
    /// <returns>List of links pointing to the note</returns>
    IReadOnlyList<NoteLink> GetBacklinksToNote(Guid noteId);

    /// <summary>
    /// Builds the complete graph of all notes and their connections
    /// </summary>
    /// <returns>The note graph with nodes and edges</returns>
    Task<NoteGraph> BuildGraphAsync();

    /// <summary>
    /// Creates link markup in the format [[note-id|display-text]]
    /// </summary>
    /// <param name="targetNoteId">The ID of the note to link to</param>
    /// <param name="displayText">Optional display text (uses note title if null)</param>
    /// <returns>The formatted link markup string</returns>
    string CreateLinkMarkup(Guid targetNoteId, string? displayText = null);

    /// <summary>
    /// Parses link markup and extracts the NoteLink information
    /// </summary>
    /// <param name="markup">The link markup string to parse</param>
    /// <returns>The parsed NoteLink or null if invalid format</returns>
    NoteLink? ParseLinkMarkup(string markup);

    /// <summary>
    /// Parses all links from note content
    /// </summary>
    /// <param name="content">The note content to parse</param>
    /// <param name="sourceNoteId">The ID of the source note</param>
    /// <returns>List of all links found in the content</returns>
    IReadOnlyList<NoteLink> ParseLinksFromContent(string content, Guid sourceNoteId);

    /// <summary>
    /// Updates display text in all links pointing to a note when its title changes
    /// </summary>
    /// <param name="noteId">The note whose title changed</param>
    /// <param name="oldTitle">The previous title</param>
    /// <param name="newTitle">The new title</param>
    Task UpdateLinksOnNoteTitleChangeAsync(Guid noteId, string oldTitle, string newTitle);

    /// <summary>
    /// Marks all links to a deleted note as broken
    /// </summary>
    /// <param name="deletedNoteId">The ID of the deleted note</param>
    Task MarkBrokenLinksAsync(Guid deletedNoteId);
}
