namespace DevSticky.Models;

/// <summary>
/// Root data container for application persistence
/// </summary>
public class AppData
{
    public AppSettings AppSettings { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<NoteGroup> Groups { get; set; } = new();
    public List<NoteTag> Tags { get; set; } = new();
}
