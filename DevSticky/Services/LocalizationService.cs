using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace DevSticky.Services;

/// <summary>
/// Service for managing application localization/i18n
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    private static readonly object _lock = new();
    
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public static LocalizationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new LocalizationService();
                }
            }
            return _instance;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Event fired when language changes - useful for updating non-XAML UI elements
    /// </summary>
    public event EventHandler? LanguageChanged;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                CultureInfo.CurrentUICulture = value;
                // Notify all bindings to refresh by using "Item[]" for indexer
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private LocalizationService()
    {
        _resourceManager = new ResourceManager("DevSticky.Resources.Strings", typeof(LocalizationService).Assembly);
        _currentCulture = CultureInfo.CurrentUICulture;
    }

    /// <summary>
    /// Get localized string by key
    /// </summary>
    public string this[string key] => GetString(key);

    /// <summary>
    /// Get localized string by key
    /// </summary>
    public string GetString(string key)
    {
        try
        {
            return _resourceManager.GetString(key, _currentCulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    /// <summary>
    /// Get localized string with format parameters
    /// </summary>
    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            return format;
        }
    }

    /// <summary>
    /// Available cultures
    /// </summary>
    public static readonly CultureInfo[] SupportedCultures = new[]
    {
        new CultureInfo("en"),
        new CultureInfo("vi")
    };

    /// <summary>
    /// Set culture by language code
    /// </summary>
    public void SetCulture(string cultureCode)
    {
        try
        {
            CurrentCulture = new CultureInfo(cultureCode);
        }
        catch
        {
            CurrentCulture = new CultureInfo("en");
        }
    }
}

/// <summary>
/// Static helper for quick access to localized strings
/// </summary>
public static class L
{
    public static string Get(string key) => LocalizationService.Instance.GetString(key);
    public static string Get(string key, params object[] args) => LocalizationService.Instance.GetString(key, args);
}
