using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace DevSticky.Views;

public partial class KeyboardShortcutsWindow : Window
{
    public KeyboardShortcutsWindow()
    {
        InitializeComponent();
        LoadShortcuts();
    }
    
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }
    
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    
    private void LoadShortcuts()
    {
        // General shortcuts
        AddCategory(L.Get("ShortcutCategoryGeneral"));
        AddShortcut("Ctrl+N", L.Get("ShortcutNewNote"));
        AddShortcut("Ctrl+S", L.Get("ShortcutSave"));
        AddShortcut("Ctrl+F", L.Get("ShortcutSearch"));
        AddShortcut("Ctrl+W", L.Get("ShortcutClose"));
        AddShortcut("Escape", L.Get("ShortcutHide"));
        
        // Snippet shortcuts
        AddCategory(L.Get("ShortcutCategorySnippets"));
        AddShortcut("Ctrl+Shift+S", L.Get("ShortcutSaveSnippet"));
        AddShortcut("Ctrl+Shift+I", L.Get("ShortcutInsertSnippet"));
        AddShortcut("Tab", L.Get("ShortcutNextPlaceholder"));
        AddShortcut("Shift+Tab", L.Get("ShortcutPrevPlaceholder"));
        
        // Formatting shortcuts
        AddCategory(L.Get("ShortcutCategoryFormat"));
        AddShortcut("Ctrl+Shift+F", L.Get("ShortcutFormat"));
        
        // Navigation shortcuts
        AddCategory(L.Get("ShortcutCategoryNavigation"));
        AddShortcut("[[", L.Get("ShortcutCreateLink"));
        AddShortcut("Ctrl+Click", L.Get("ShortcutFollowLink"));
        
        // Help shortcuts
        AddCategory(L.Get("ShortcutCategoryHelp"));
        AddShortcut("F1", L.Get("ShortcutHelp"));
    }
    
    private void AddCategory(string name)
    {
        ShortcutsPanel.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("BlueBrush"),
            Margin = new Thickness(0, ShortcutsPanel.Children.Count > 0 ? 16 : 0, 0, 8)
        });
    }
    
    private void AddShortcut(string key, string description)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        var keyBorder = new Border
        {
            Background = (Brush)FindResource("Surface1Brush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        keyBorder.Child = new TextBlock
        {
            Text = key,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)FindResource("GreenBrush")
        };
        Grid.SetColumn(keyBorder, 0);
        grid.Children.Add(keyBorder);
        
        var descText = new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(descText, 1);
        grid.Children.Add(descText);
        
        ShortcutsPanel.Children.Add(grid);
    }
}
