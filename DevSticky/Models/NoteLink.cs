namespace DevSticky.Models;

/// <summary>
/// Represents a link from one note to another note within the application
/// </summary>
public class NoteLink
{
    public Guid SourceNoteId { get; set; }
    public Guid TargetNoteId { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int Length { get; set; }
    public bool IsBroken { get; set; }
}
