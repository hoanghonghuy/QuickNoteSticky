namespace DevSticky.Models;

/// <summary>
/// Represents an edge (connection) between two notes in the graph
/// </summary>
public class NoteGraphEdge
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
}
