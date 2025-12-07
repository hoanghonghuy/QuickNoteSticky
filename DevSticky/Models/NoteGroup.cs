namespace DevSticky.Models;

/// <summary>
/// Model for grouping notes together
/// </summary>
public class NoteGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Group";
    public bool IsExpanded { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public string DisplayName => Name.Length > 30 ? Name[..30] : Name;
}
