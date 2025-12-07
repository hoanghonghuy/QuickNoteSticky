namespace DevSticky.Models;

/// <summary>
/// Represents the complete graph of notes and their connections
/// </summary>
public class NoteGraph
{
    public IReadOnlyList<NoteGraphNode> Nodes { get; set; } = Array.Empty<NoteGraphNode>();
    public IReadOnlyList<NoteGraphEdge> Edges { get; set; } = Array.Empty<NoteGraphEdge>();
}
