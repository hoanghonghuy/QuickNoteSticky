namespace DevSticky.Models;

/// <summary>
/// Represents a node in the note graph visualization
/// </summary>
public class NoteGraphNode
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int LinkCount { get; set; }
    public int BacklinkCount { get; set; }
}
