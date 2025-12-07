namespace DevSticky.Interfaces;

/// <summary>
/// Service for debouncing operations (e.g., auto-save)
/// </summary>
public interface IDebounceService
{
    void Debounce(string key, Action action, int milliseconds);
    void Cancel(string key);
}
