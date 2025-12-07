using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Resources;
using DevSticky.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DevSticky.Views;

public partial class NoteWindow : Window
{
    private NoteViewModel? _viewModel;
    private bool _isPinned = true;
    private IThemeService? _themeService;
    private string _currentLanguage = "PlainText";

    public NoteWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
        
        LanguageCombo.ItemsSource = new[] 
        { 
            "PlainText", "CSharp", "Java", "JavaScript", "TypeScript", 
            "Json", "Xml", "Sql", "Python", "Bash" 
        };
        LanguageCombo.SelectedIndex = 0;
        
        // Subscribe to theme changes for syntax highlighting
        try
        {
            _themeService = App.GetService<IThemeService>();
            _themeService.ThemeChanged += OnThemeChanged;
        }
        catch { /* Service not available during design time */ }
    }
    
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        // Re-apply syntax highlighting with new theme colors
        Dispatcher.Invoke(() => ApplySyntaxHighlighting(_currentLanguage));
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is NoteViewModel vm)
        {
            _viewModel = vm;
            _isPinned = vm.IsPinned;
            
            Editor.Text = vm.Content;
            Editor.TextChanged += (_, _) => 
            {
                if (_viewModel != null)
                    _viewModel.Content = Editor.Text;
            };
            
            Opacity = vm.Opacity;
            Topmost = vm.IsPinned;
            OpacitySlider.Value = vm.Opacity;
            
            var langIndex = Array.IndexOf(
                new[] { "PlainText", "CSharp", "Java", "JavaScript", "TypeScript", "Json", "Xml", "Sql", "Python", "Bash" },
                vm.Language);
            if (langIndex >= 0) LanguageCombo.SelectedIndex = langIndex;
            
            ApplySyntaxHighlighting(vm.Language);
            
            LanguageCombo.SelectionChanged += (_, _) =>
            {
                if (LanguageCombo.SelectedItem is string lang && _viewModel != null)
                {
                    _viewModel.Language = lang;
                    ApplySyntaxHighlighting(lang);
                }
            };
            
            UpdatePinButton();
        }
    }


    private void ApplySyntaxHighlighting(string language)
    {
        _currentLanguage = language;
        
        var highlighting = language switch
        {
            "CSharp" => "C#",
            "JavaScript" or "TypeScript" => "JavaScript",
            "Json" => "Json",
            "Xml" => "XML",
            "Sql" => "TSQL",
            "Python" => "Python",
            "Java" => "Java",
            "Bash" => "Boo", // Use Boo as fallback for shell-like syntax
            _ => null
        };
        
        if (highlighting != null)
        {
            try
            {
                var definition = HighlightingManager.Instance.GetDefinition(highlighting);
                if (definition != null)
                {
                    // Apply theme-appropriate syntax highlighting colors
                    var isDarkTheme = _themeService?.CurrentTheme == Models.Theme.Dark;
                    if (isDarkTheme != false)
                    {
                        VSCodeDarkTheme.ApplyTheme(definition);
                    }
                    else
                    {
                        VSCodeLightTheme.ApplyTheme(definition);
                    }
                    Editor.SyntaxHighlighting = definition;
                }
            }
            catch { Editor.SyntaxHighlighting = null; }
        }
        else
        {
            Editor.SyntaxHighlighting = null;
        }
    }

    private void UpdatePinButton()
    {
        BtnPin.Content = _isPinned ? "●" : "○";
        BtnPin.ToolTip = _isPinned ? "Unpin (on top)" : "Pin";
    }

    // Event Handlers
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        
        // Force Topmost change by toggling it
        Topmost = false;
        if (_isPinned)
        {
            Topmost = true;
        }
        
        if (_viewModel != null)
            _viewModel.IsPinned = _isPinned;
        
        UpdatePinButton();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchPanel.Visibility = SearchPanel.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
        if (SearchPanel.Visibility == Visibility.Visible)
            SearchBox.Focus();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseCommand.Execute(null);
        }
        else
        {
            Close();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.SearchTerm = SearchBox.Text;
    }

    private void BtnPrevMatch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.PreviousMatchCommand.Execute(null);
    }

    private void BtnNextMatch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.NextMatchCommand.Execute(null);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        if (_viewModel != null)
            _viewModel.Opacity = e.NewValue;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S:
                    _viewModel.SaveCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F:
                    BtnSearch_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.W:
                    BtnClose_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.F)
            {
                _viewModel.FormatCommand.Execute(null);
                Editor.Text = _viewModel.Content;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (SearchPanel.Visibility == Visibility.Visible)
                SearchPanel.Visibility = Visibility.Collapsed;
            else
                Hide();
            e.Handled = true;
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_viewModel != null)
        {
            _viewModel.WindowRect.Top = Top;
            _viewModel.WindowRect.Left = Left;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_viewModel != null)
        {
            _viewModel.WindowRect.Width = ActualWidth;
            _viewModel.WindowRect.Height = ActualHeight;
        }
    }

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var newWidth = Width + e.HorizontalChange;
        var newHeight = Height + e.VerticalChange;
        
        if (newWidth >= MinWidth)
            Width = newWidth;
        if (newHeight >= MinHeight)
            Height = newHeight;
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from theme changes
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
        base.OnClosed(e);
    }
}
