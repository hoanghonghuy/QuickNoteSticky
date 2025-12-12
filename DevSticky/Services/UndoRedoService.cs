using DevSticky.Interfaces;

namespace DevSticky.Services;

/// <summary>
/// Generic undo/redo service using command pattern.
/// Note: For AvalonEdit, use its built-in UndoStack instead.
/// This service is for custom undo/redo operations outside the editor.
/// </summary>
public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    private int _maxUndoSteps = 50;

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public int MaxUndoSteps
    {
        get => _maxUndoSteps;
        set
        {
            _maxUndoSteps = Math.Max(1, Math.Min(200, value));
            TrimUndoStack();
        }
    }

    /// <summary>
    /// Executes an action and adds it to the undo stack
    /// </summary>
    public void Execute(IUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
        TrimUndoStack();
        OnStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        OnStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
        OnStateChanged();
    }

    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    private void TrimUndoStack()
    {
        while (_undoStack.Count > _maxUndoSteps)
        {
            // Remove oldest items (bottom of stack)
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < _maxUndoSteps; i++)
            {
                _undoStack.Push(items[items.Length - 1 - i]);
            }
        }
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Interface for undoable actions
/// </summary>
public interface IUndoableAction
{
    /// <summary>
    /// Executes the action
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the action
    /// </summary>
    void Undo();

    /// <summary>
    /// Description of the action for display
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Simple action that stores before/after states
/// </summary>
public class SimpleUndoableAction<T> : IUndoableAction
{
    private readonly T _beforeState;
    private readonly T _afterState;
    private readonly Action<T> _applyState;

    public string Description { get; }

    public SimpleUndoableAction(T beforeState, T afterState, Action<T> applyState, string description = "")
    {
        _beforeState = beforeState;
        _afterState = afterState;
        _applyState = applyState;
        Description = description;
    }

    public void Execute() => _applyState(_afterState);
    public void Undo() => _applyState(_beforeState);
}

/// <summary>
/// Action using delegates for execute and undo
/// </summary>
public class DelegateUndoableAction : IUndoableAction
{
    private readonly Action _execute;
    private readonly Action _undo;

    public string Description { get; }

    public DelegateUndoableAction(Action execute, Action undo, string description = "")
    {
        _execute = execute;
        _undo = undo;
        Description = description;
    }

    public void Execute() => _execute();
    public void Undo() => _undo();
}
