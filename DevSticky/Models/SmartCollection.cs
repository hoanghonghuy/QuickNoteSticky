namespace DevSticky.Models;

/// <summary>
/// Represents a smart collection that automatically groups notes by criteria
/// </summary>
public class SmartCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public FilterCriteria Criteria { get; set; } = new();
    public bool IsBuiltIn { get; set; }
}