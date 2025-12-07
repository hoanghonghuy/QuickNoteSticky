namespace DevSticky.Models;

/// <summary>
/// Model for tagging notes
/// </summary>
public class NoteTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Tag";
    public string Color { get; set; } = "#89B4FA"; // Default blue
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    public string DisplayName => Name.Length > 20 ? Name[..20] : Name;
    
    // Predefined colors for quick selection
    public static readonly string[] DefaultColors = new[]
    {
        "#89B4FA", // Blue
        "#A6E3A1", // Green
        "#F9E2AF", // Yellow
        "#FAB387", // Peach
        "#F38BA8", // Red
        "#CBA6F7", // Mauve
        "#94E2D5", // Teal
        "#F5C2E7", // Pink
    };
}
