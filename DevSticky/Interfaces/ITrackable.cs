namespace DevSticky.Interfaces;

/// <summary>
/// Interface for objects that support dirty tracking
/// </summary>
public interface ITrackable
{
    /// <summary>
    /// Gets or sets whether the object has been modified since last save
    /// </summary>
    bool IsDirty { get; set; }

    /// <summary>
    /// Marks the object as clean (not modified)
    /// </summary>
    void MarkClean();

    /// <summary>
    /// Marks the object as dirty (modified)
    /// </summary>
    void MarkDirty();
}
