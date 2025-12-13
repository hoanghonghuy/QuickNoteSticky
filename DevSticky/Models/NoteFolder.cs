namespace DevSticky.Models;

/// <summary>
/// Represents a folder that can contain notes and other folders
/// </summary>
public class NoteFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Folder";
    public Guid? ParentId { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
}