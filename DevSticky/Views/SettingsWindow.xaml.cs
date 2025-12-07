using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;

namespace DevSticky.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly IThemeService? _themeService;
    private bool _isLoading;
    
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        
        // Try to get theme service from DI container
        try
        {
            _themeService = App.GetService<IThemeService>();
        }
        catch
        {
            // Theme service may not be registered yet
            _themeService = null;
        }
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        
        OpacitySlider.Value = _settings.DefaultOpacity;
        FontSizeSlider.Value = _settings.DefaultFontSize;
        WidthSlider.Value = _settings.DefaultWidth;
        HeightSlider.Value = _settings.DefaultHeight;
        AutoSaveSlider.Value = _settings.AutoSaveDelayMs;
        
        // Set language selection
        foreach (ComboBoxItem item in LanguageCombo.Items)
        {
            if (item.Tag?.ToString() == _settings.Language)
            {
                LanguageCombo.SelectedItem = item;
                break;
            }
        }
        
        // Set theme selection
        foreach (ComboBoxItem item in ThemeCombo.Items)
        {
            if (item.Tag?.ToString() == _settings.ThemeMode)
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }
        
        _isLoading = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        _settings.DefaultOpacity = OpacitySlider.Value;
        _settings.DefaultFontSize = (int)FontSizeSlider.Value;
        _settings.DefaultWidth = (int)WidthSlider.Value;
        _settings.DefaultHeight = (int)HeightSlider.Value;
        _settings.AutoSaveDelayMs = (int)AutoSaveSlider.Value;
        
        // Save language
        if (LanguageCombo.SelectedItem is ComboBoxItem selectedLang)
        {
            _settings.Language = selectedLang.Tag?.ToString() ?? "en";
            LocalizationService.Instance.SetCulture(_settings.Language);
        }
        
        // Save theme mode
        if (ThemeCombo.SelectedItem is ComboBoxItem selectedTheme)
        {
            _settings.ThemeMode = selectedTheme.Tag?.ToString() ?? "System";
        }
        
        // Save to file
        _settings.Save();
        
        Close();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        
        // Preview language change immediately
        if (LanguageCombo.SelectedItem is ComboBoxItem selectedLang)
        {
            var langCode = selectedLang.Tag?.ToString() ?? "en";
            LocalizationService.Instance.SetCulture(langCode);
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        
        // Apply theme change immediately
        if (ThemeCombo.SelectedItem is ComboBoxItem selectedTheme && _themeService != null)
        {
            var themeModeStr = selectedTheme.Tag?.ToString() ?? "System";
            var themeMode = themeModeStr switch
            {
                "Light" => ThemeMode.Light,
                "Dark" => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            _themeService.SetThemeMode(themeMode);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValue != null)
            OpacityValue.Text = e.NewValue.ToString("F2");
    }

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FontSizeValue != null)
            FontSizeValue.Text = ((int)e.NewValue).ToString();
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthValue != null)
            WidthValue.Text = ((int)e.NewValue).ToString();
    }

    private void HeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HeightValue != null)
            HeightValue.Text = ((int)e.NewValue).ToString();
    }

    private void AutoSaveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AutoSaveValue != null)
            AutoSaveValue.Text = ((int)e.NewValue).ToString();
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "DevSticky Backup (*.devsticky)|*.devsticky|JSON Files (*.json)|*.json",
            DefaultExt = ".devsticky",
            FileName = $"DevSticky_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var storageService = App.GetService<Interfaces.IStorageService>();
                var data = await storageService.LoadAsync();
                
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json);
                
                CustomDialog.ShowSuccess(L.Get("Success"), L.Get("ExportSuccess"), this);
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError(L.Get("Error"), $"{L.Get("ExportError")}\n\n{ex.Message}", this);
            }
        }
    }

    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "DevSticky Backup (*.devsticky)|*.devsticky|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".devsticky"
        };

        if (dialog.ShowDialog() == true)
        {
            // Confirm before import
            if (!CustomDialog.ConfirmWarning(L.Get("ImportConfirmTitle"), L.Get("ImportConfirm"), this))
                return;

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
                var data = System.Text.Json.JsonSerializer.Deserialize<Models.AppData>(json);
                
                if (data == null)
                {
                    throw new Exception("Invalid backup file format.");
                }

                var storageService = App.GetService<Interfaces.IStorageService>();
                await storageService.SaveAsync(data);
                
                CustomDialog.ShowSuccess(L.Get("Success"), L.Get("ImportSuccess"), this);
                
                // Close settings and suggest restart
                Close();
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError(L.Get("Error"), $"{L.Get("ImportError")}\n\n{ex.Message}", this);
            }
        }
    }
}
