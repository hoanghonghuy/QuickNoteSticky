namespace DevSticky.Models;

/// <summary>
/// Defines filter criteria for smart collections
/// </summary>
public class FilterCriteria
{
    public List<Guid>? TagIds { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public DateRangeType? DateRange { get; set; }
    public string? ContentPattern { get; set; }
    public bool? HasCodeBlocks { get; set; }
    public bool? HasUncheckedTodos { get; set; }
}