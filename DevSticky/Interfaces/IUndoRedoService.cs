namespace DevSticky.Interfaces;

/// <summary>
/// Service for managing undo/redo operations
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// Whether undo is available
    /// </summary>
    bool CanUndo { get; }
    
    /// <summary>
    /// Whether redo is available
    /// </summary>
    bool CanRedo { get; }
    
    /// <summary>
    /// Performs undo operation
    /// </summary>
    void Undo();
    
    /// <summary>
    /// Performs redo operation
    /// </summary>
    void Redo();
    
    /// <summary>
    /// Clears undo/redo history
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Gets the number of undo steps available
    /// </summary>
    int UndoCount { get; }
    
    /// <summary>
    /// Gets the number of redo steps available
    /// </summary>
    int RedoCount { get; }
    
    /// <summary>
    /// Maximum number of undo steps to keep
    /// </summary>
    int MaxUndoSteps { get; set; }
    
    /// <summary>
    /// Event raised when undo/redo state changes
    /// </summary>
    event EventHandler? StateChanged;
}
