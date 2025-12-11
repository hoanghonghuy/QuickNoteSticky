using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.Helpers;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Application = System.Windows.Application;

namespace DevSticky.Views;

public partial class SettingsWindow : Window, IDisposable
{
    private readonly AppSettings _settings;
    private readonly IThemeService? _themeService;
    private readonly IHotkeyService? _hotkeyService;
    private readonly ICloudConnection? _cloudConnection;
    private readonly ICloudSync? _cloudSync;
    private readonly ICloudEncryption? _cloudEncryption;
    private readonly IEncryptionService? _encryptionService;
    private bool _isLoading;
    private System.Windows.Controls.TextBox? _activeHotkeyBox;
    private bool _isPassphraseVisible;
    private string _currentPassphrase = string.Empty;
    private readonly EventSubscriptionManager _eventManager = new();
    private bool _disposed;
    
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        
        // Try to get services from DI container
        try
        {
            _themeService = App.GetService<IThemeService>();
            _hotkeyService = App.GetService<IHotkeyService>();
        }
        catch
        {
            // Services may not be registered yet
            _themeService = null;
            _hotkeyService = null;
        }
        
        // Try to get cloud sync services
        try
        {
            _cloudConnection = App.GetService<ICloudConnection>();
            _cloudSync = App.GetService<ICloudSync>();
            _cloudEncryption = App.GetService<ICloudEncryption>();
            _encryptionService = App.GetService<IEncryptionService>();
            
            // Subscribe to sync events
            if (_cloudSync != null)
            {
                _eventManager.Subscribe<SyncProgressEventArgs>(_cloudSync, nameof(_cloudSync.SyncProgress), OnSyncProgress);
            }
        }
        catch
        {
            // Cloud services not available
            _encryptionService = null;
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
        
        // Load hotkey settings
        NewNoteHotkeyBox.Text = _settings.Hotkeys.NewNoteHotkey;
        ToggleVisibilityHotkeyBox.Text = _settings.Hotkeys.ToggleVisibilityHotkey;
        QuickCaptureHotkeyBox.Text = _settings.Hotkeys.QuickCaptureHotkey;
        
        // Load cloud sync settings (Requirements 5.1, 5.10, 5.11)
        LoadCloudSyncSettings();
        
        _isLoading = false;
    }
    
    /// <summary>
    /// Load cloud sync settings into UI controls (Requirements 5.1, 5.10, 5.11)
    /// </summary>
    private void LoadCloudSyncSettings()
    {
        // Set cloud provider selection
        var providerTag = _settings.CloudSync.Provider?.ToString() ?? "None";
        foreach (ComboBoxItem item in CloudProviderCombo.Items)
        {
            if (item.Tag?.ToString() == providerTag)
            {
                CloudProviderCombo.SelectedItem = item;
                break;
            }
        }
        
        // Set sync interval (convert seconds to minutes)
        SyncIntervalSlider.Value = _settings.CloudSync.SyncIntervalSeconds / 60;
        
        // Update cloud status display
        UpdateCloudSyncStatus();
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
        
        // Save hotkey settings (Requirements 1.7)
        _settings.Hotkeys.NewNoteHotkey = NewNoteHotkeyBox.Text;
        _settings.Hotkeys.ToggleVisibilityHotkey = ToggleVisibilityHotkeyBox.Text;
        _settings.Hotkeys.QuickCaptureHotkey = QuickCaptureHotkeyBox.Text;
        
        // Save cloud sync settings (Requirements 5.1, 5.10, 5.11)
        _settings.CloudSync.SyncIntervalSeconds = (int)SyncIntervalSlider.Value * 60;
        var selectedProvider = GetSelectedCloudProvider();
        if (selectedProvider.HasValue && _cloudConnection?.Status == SyncStatus.Idle)
        {
            _settings.CloudSync.Provider = selectedProvider;
        }
        
        // Store encryption passphrase hash if provided (Requirements 5.11)
        if (!string.IsNullOrEmpty(_currentPassphrase) && _encryptionService != null)
        {
            // Store a hash of the passphrase for verification (not the passphrase itself)
            _settings.EncryptionPassphraseHash = ComputePassphraseHash(_currentPassphrase);
        }
        
        // Save to file
        _settings.Save();
        
        // Re-register hotkeys with new settings (Requirements 1.7)
        try
        {
            if (Application.Current is App app)
            {
                app.ReregisterHotkeys();
            }
        }
        catch
        {
            // Ignore errors during hotkey re-registration
        }
        
        Close();
    }
    
    /// <summary>
    /// Compute a hash of the passphrase for storage verification
    /// </summary>
    private static string ComputePassphraseHash(string passphrase)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(passphrase);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        
        // Preview language change immediately
        if (LanguageCombo.SelectedItem is ComboBoxItem selectedLang)
        {
            var langCode = selectedLang.Tag?.ToString() ?? "en";
            LocalizationService.Instance.SetCulture(langCode);
            
            // Update dynamically set labels that don't use XAML binding
            UpdateCloudSyncStatus();
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
            Filter = L.Get("FilterBackupFiles"),
            DefaultExt = ".devsticky",
            FileName = L.Get("BackupFileNameFormat", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
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
                
                await System.IO.File.WriteAllTextAsync(dialog.FileName, json).ConfigureAwait(false);
                
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
            Filter = L.Get("FilterBackupFilesAll"),
            DefaultExt = ".devsticky"
        };

        if (dialog.ShowDialog() == true)
        {
            // Confirm before import
            if (!CustomDialog.ConfirmWarning(L.Get("ImportConfirmTitle"), L.Get("ImportConfirm"), this))
                return;

            try
            {
                var json = await System.IO.File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(false);
                var data = System.Text.Json.JsonSerializer.Deserialize<Models.AppData>(json);
                
                if (data == null)
                {
                    throw new Exception(L.Get("InvalidBackupFormat"));
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

    #region Hotkey Configuration (Requirements 1.6, 1.7)

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            _activeHotkeyBox = textBox;
            textBox.Background = FindResource("Surface2Brush") as Brush;
            HotkeyStatusText.Text = L.Get("HotkeyHint");
            HotkeyStatusText.Foreground = FindResource("SubtextBrush") as Brush;
        }
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            _activeHotkeyBox = null;
            textBox.Background = FindResource("Surface1Brush") as Brush;
            HotkeyStatusText.Text = "";
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;
        
        e.Handled = true;

        // Get the actual key (handle system keys)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Get modifiers
        var modifiers = Keyboard.Modifiers;

        // Require at least one modifier for global hotkeys
        if (modifiers == ModifierKeys.None)
        {
            HotkeyStatusText.Text = L.Get("HotkeyHint");
            HotkeyStatusText.Foreground = FindResource("YellowBrush") as Brush ?? Brushes.Yellow;
            return;
        }

        // Format the hotkey string
        var hotkeyString = FormatHotkey(modifiers, key);
        
        // Check if hotkey is available (Requirements 1.6)
        bool isAvailable = CheckHotkeyAvailability(modifiers, key, textBox);
        
        if (isAvailable)
        {
            textBox.Text = hotkeyString;
            HotkeyStatusText.Text = L.Get("HotkeyAvailable");
            HotkeyStatusText.Foreground = FindResource("GreenBrush") as Brush ?? Brushes.Green;
        }
        else
        {
            textBox.Text = hotkeyString;
            HotkeyStatusText.Text = L.Get("HotkeyInUse");
            HotkeyStatusText.Foreground = FindResource("RedBrush") as Brush ?? Brushes.Red;
        }
    }

    private bool CheckHotkeyAvailability(ModifierKeys modifiers, Key key, System.Windows.Controls.TextBox currentBox)
    {
        // Check against other hotkey boxes in this window
        var hotkeyString = FormatHotkey(modifiers, key);
        
        if (currentBox != NewNoteHotkeyBox && NewNoteHotkeyBox.Text == hotkeyString)
            return false;
        if (currentBox != ToggleVisibilityHotkeyBox && ToggleVisibilityHotkeyBox.Text == hotkeyString)
            return false;
        if (currentBox != QuickCaptureHotkeyBox && QuickCaptureHotkeyBox.Text == hotkeyString)
            return false;

        // Check system-wide availability using the hotkey service
        if (_hotkeyService != null)
        {
            return _hotkeyService.IsHotkeyAvailable(modifiers, key);
        }

        return true;
    }

    private static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void ClearNewNoteHotkey_Click(object sender, RoutedEventArgs e)
    {
        NewNoteHotkeyBox.Text = "";
    }

    private void ClearToggleVisibilityHotkey_Click(object sender, RoutedEventArgs e)
    {
        ToggleVisibilityHotkeyBox.Text = "";
    }

    private void ClearQuickCaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        QuickCaptureHotkeyBox.Text = "";
    }

    #endregion

    #region Cloud Sync Configuration (Requirements 5.1, 5.10, 5.11)

    /// <summary>
    /// Handle cloud provider selection change (Requirements 5.1)
    /// </summary>
    private void CloudProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        
        UpdateCloudSyncStatus();
    }

    /// <summary>
    /// Handle connect/disconnect button click (Requirements 5.1, 5.10)
    /// </summary>
    private async void CloudConnectBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_cloudConnection == null)
        {
            CustomDialog.ShowError(L.Get("Error"), L.Get("CloudSyncNotAvailable"), this);
            return;
        }

        var selectedProvider = GetSelectedCloudProvider();
        
        if (_cloudConnection?.Status == SyncStatus.Idle || _cloudConnection?.Status == SyncStatus.Error)
        {
            // Disconnect (Requirements 5.10)
            await _cloudConnection.DisconnectAsync();
            _settings.CloudSync.IsEnabled = false;
            _settings.CloudSync.Provider = null;
            UpdateCloudSyncStatus();
        }
        else if (selectedProvider.HasValue)
        {
            // Connect to selected provider (Requirements 5.1)
            CloudConnectBtn.IsEnabled = false;
            CloudConnectBtn.Content = L.Get("CloudConnecting");
            
            try
            {
                // Set encryption passphrase if provided (Requirements 5.11)
                if (!string.IsNullOrEmpty(_currentPassphrase))
                {
                    _cloudEncryption?.SetEncryptionPassphrase(_currentPassphrase);
                }
                
                var success = await _cloudConnection!.ConnectAsync(selectedProvider.Value);
                
                if (success)
                {
                    _settings.CloudSync.IsEnabled = true;
                    _settings.CloudSync.Provider = selectedProvider;
                    CustomDialog.ShowSuccess(L.Get("Success"), L.Get("CloudConnectSuccess"), this);
                }
                else
                {
                    CustomDialog.ShowError(L.Get("Error"), L.Get("CloudConnectFailed"), this);
                }
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError(L.Get("Error"), $"{L.Get("CloudConnectFailed")}\n\n{ex.Message}", this);
            }
            finally
            {
                CloudConnectBtn.IsEnabled = true;
                UpdateCloudSyncStatus();
            }
        }
        else
        {
            CustomDialog.ShowInfo(L.Get("Info"), L.Get("CloudSelectProvider"), this);
        }
    }

    /// <summary>
    /// Get the selected cloud provider from the combo box
    /// </summary>
    private CloudProvider? GetSelectedCloudProvider()
    {
        if (CloudProviderCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            return selectedItem.Tag?.ToString() switch
            {
                "OneDrive" => CloudProvider.OneDrive,
                "GoogleDrive" => CloudProvider.GoogleDrive,
                _ => null
            };
        }
        return null;
    }

    /// <summary>
    /// Update the cloud sync status display
    /// </summary>
    private void UpdateCloudSyncStatus()
    {
        if (_cloudConnection == null)
        {
            CloudStatusText.Text = L.Get("CloudSyncNotAvailable");
            CloudConnectBtn.Content = L.Get("CloudConnect");
            CloudConnectBtn.IsEnabled = false;
            return;
        }

        var selectedProvider = GetSelectedCloudProvider();
        
        switch (_cloudConnection?.Status ?? SyncStatus.Disconnected)
        {
            case SyncStatus.Disconnected:
                CloudStatusText.Text = L.Get("CloudStatusDisconnected");
                CloudConnectBtn.Content = L.Get("CloudConnect");
                CloudConnectBtn.IsEnabled = selectedProvider.HasValue;
                break;
                
            case SyncStatus.Connecting:
                CloudStatusText.Text = L.Get("CloudStatusConnecting");
                CloudConnectBtn.Content = L.Get("CloudConnecting");
                CloudConnectBtn.IsEnabled = false;
                break;
                
            case SyncStatus.Syncing:
                CloudStatusText.Text = L.Get("CloudStatusSyncing");
                CloudConnectBtn.Content = L.Get("CloudDisconnect");
                CloudConnectBtn.IsEnabled = false;
                break;
                
            case SyncStatus.Idle:
                var lastSync = _cloudSync?.LastSyncResult?.CompletedAt;
                if (lastSync.HasValue)
                {
                    CloudStatusText.Text = string.Format(L.Get("CloudStatusConnectedLastSync"), 
                        lastSync.Value.ToLocalTime().ToString("g", LocalizationService.Instance.CurrentCulture));
                }
                else
                {
                    CloudStatusText.Text = L.Get("CloudStatusConnected");
                }
                CloudConnectBtn.Content = L.Get("CloudDisconnect");
                CloudConnectBtn.IsEnabled = true;
                break;
                
            case SyncStatus.Error:
                CloudStatusText.Text = L.Get("CloudStatusError");
                CloudStatusText.Foreground = FindResource("RedBrush") as Brush ?? Brushes.Red;
                CloudConnectBtn.Content = L.Get("CloudDisconnect");
                CloudConnectBtn.IsEnabled = true;
                break;
        }
    }

    /// <summary>
    /// Handle encryption passphrase change (Requirements 5.11)
    /// </summary>
    private void EncryptionPassphraseBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _currentPassphrase = EncryptionPassphraseBox.Password;
    }

    /// <summary>
    /// Toggle passphrase visibility
    /// </summary>
    private void TogglePassphraseVisibility_Click(object sender, RoutedEventArgs e)
    {
        // Note: WPF PasswordBox doesn't support showing password directly
        // This would require a custom implementation with TextBox toggle
        // For now, just show a tooltip with the password length
        _isPassphraseVisible = !_isPassphraseVisible;
        
        if (_isPassphraseVisible && !string.IsNullOrEmpty(_currentPassphrase))
        {
            CustomDialog.ShowInfo(L.Get("Info"), 
                string.Format(L.Get("PassphraseLength"), _currentPassphrase.Length), this);
        }
    }

    /// <summary>
    /// Handle sync interval slider change
    /// </summary>
    private void SyncIntervalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SyncIntervalValue != null)
            SyncIntervalValue.Text = ((int)e.NewValue).ToString();
    }

    /// <summary>
    /// Handle sync progress events
    /// </summary>
    private void OnSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            CloudSyncStatusText.Text = $"{e.Operation}: {e.ProgressPercent}% - {e.Message}";
        });
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _eventManager?.Dispose();
        GC.SuppressFinalize(this);
    }
}
