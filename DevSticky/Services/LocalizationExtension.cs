using System.Windows.Markup;

namespace DevSticky.Services;

/// <summary>
/// Markup extension for localization in XAML
/// Usage: {loc:Localize Dashboard} or {loc:Localize Key=Dashboard}
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }
    public LocalizeExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new System.Windows.Data.Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = System.Windows.Data.BindingMode.OneWay
        };
        
        return binding.ProvideValue(serviceProvider);
    }
}
