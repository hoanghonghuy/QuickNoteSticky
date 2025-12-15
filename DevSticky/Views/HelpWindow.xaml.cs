using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Services;
using Brush = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace DevSticky.Views;

public partial class HelpWindow : Window
{
    private readonly Dictionary<string, Action> _sections;
    
    public HelpWindow()
    {
        InitializeComponent();
        
        _sections = new Dictionary<string, Action>
        {
            { "🚀 " + L.Get("HelpQuickStart"), ShowQuickStart },
            { "📝 " + L.Get("HelpNotes"), ShowNotesHelp },
            { "📋 " + L.Get("HelpKanban"), ShowKanbanHelp },
            { "✂️ " + L.Get("HelpSnippets"), ShowSnippetsHelp },
            { "🔗 " + L.Get("HelpGraph"), ShowGraphHelp },
            { "📅 " + L.Get("HelpTimeline"), ShowTimelineHelp },
            { "⌨️ " + L.Get("HelpShortcuts"), ShowShortcutsHelp },
            { "💡 " + L.Get("HelpTips"), ShowTipsHelp }
        };
        
        foreach (var section in _sections.Keys)
        {
            NavList.Items.Add(new ListBoxItem 
            { 
                Content = section,
                Foreground = (Brush)FindResource("TextBrush")
            });
        }
        
        NavList.SelectedIndex = 0;
    }
    
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }
    
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    
    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ListBoxItem item && item.Content is string key)
        {
            var action = _sections.FirstOrDefault(s => key.Contains(s.Key.Split(' ').Last())).Value;
            action?.Invoke();
        }
    }

    #region Content Sections
    
    private void ShowQuickStart()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpQuickStartTitle"));
        AddParagraph(L.Get("HelpQuickStartIntro"));
        
        AddSubtitle("1. " + L.Get("HelpCreateNote"));
        AddParagraph(L.Get("HelpCreateNoteDesc"));
        
        AddSubtitle("2. " + L.Get("HelpOrganize"));
        AddParagraph(L.Get("HelpOrganizeDesc"));
        
        AddSubtitle("3. " + L.Get("HelpViews"));
        AddParagraph(L.Get("HelpViewsDesc"));
    }
    
    private void ShowNotesHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpNotesTitle"));
        
        AddSubtitle(L.Get("HelpNotesCreate"));
        AddBullet(L.Get("HelpNotesCreateDesc1"));
        AddBullet(L.Get("HelpNotesCreateDesc2"));
        
        AddSubtitle(L.Get("HelpNotesEdit"));
        AddBullet(L.Get("HelpNotesEditDesc1"));
        AddBullet(L.Get("HelpNotesEditDesc2"));
        AddBullet(L.Get("HelpNotesEditDesc3"));
        
        AddSubtitle(L.Get("HelpNotesFeatures"));
        AddBullet(L.Get("HelpNotesFeature1"));
        AddBullet(L.Get("HelpNotesFeature2"));
        AddBullet(L.Get("HelpNotesFeature3"));
        AddBullet(L.Get("HelpNotesFeature4"));
    }
    
    private void ShowKanbanHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpKanbanTitle"));
        AddParagraph(L.Get("HelpKanbanIntro"));
        
        AddSubtitle(L.Get("HelpKanbanOpen"));
        AddParagraph(L.Get("HelpKanbanOpenDesc"));
        
        AddSubtitle(L.Get("HelpKanbanUse"));
        AddBullet(L.Get("HelpKanbanUseDesc1"));
        AddBullet(L.Get("HelpKanbanUseDesc2"));
        AddBullet(L.Get("HelpKanbanUseDesc3"));
        
        AddSubtitle(L.Get("HelpKanbanColumns"));
        AddBullet("📋 To Do - " + L.Get("HelpKanbanTodo"));
        AddBullet("🔄 In Progress - " + L.Get("HelpKanbanProgress"));
        AddBullet("✅ Done - " + L.Get("HelpKanbanDone"));
    }
    
    private void ShowSnippetsHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpSnippetsTitle"));
        AddParagraph(L.Get("HelpSnippetsIntro"));
        
        AddSubtitle(L.Get("HelpSnippetsSave"));
        AddBullet(L.Get("HelpSnippetsSaveDesc1"));
        AddBullet(L.Get("HelpSnippetsSaveDesc2"));
        AddBullet(L.Get("HelpSnippetsSaveDesc3"));
        
        AddSubtitle(L.Get("HelpSnippetsInsert"));
        AddBullet(L.Get("HelpSnippetsInsertDesc1"));
        AddBullet(L.Get("HelpSnippetsInsertDesc2"));
        
        AddSubtitle(L.Get("HelpSnippetsManage"));
        AddParagraph(L.Get("HelpSnippetsManageDesc"));
    }
    
    private void ShowGraphHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpGraphTitle"));
        AddParagraph(L.Get("HelpGraphIntro"));
        
        AddSubtitle(L.Get("HelpGraphCreate"));
        AddBullet(L.Get("HelpGraphCreateDesc1"));
        AddBullet(L.Get("HelpGraphCreateDesc2"));
        
        AddSubtitle(L.Get("HelpGraphView"));
        AddBullet(L.Get("HelpGraphViewDesc1"));
        AddBullet(L.Get("HelpGraphViewDesc2"));
        AddBullet(L.Get("HelpGraphViewDesc3"));
    }
    
    private void ShowTimelineHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpTimelineTitle"));
        AddParagraph(L.Get("HelpTimelineIntro"));
        
        AddSubtitle(L.Get("HelpTimelineOpen"));
        AddParagraph(L.Get("HelpTimelineOpenDesc"));
        
        AddSubtitle(L.Get("HelpTimelineFeatures"));
        AddBullet(L.Get("HelpTimelineFeature1"));
        AddBullet(L.Get("HelpTimelineFeature2"));
        AddBullet(L.Get("HelpTimelineFeature3"));
    }
    
    private void ShowShortcutsHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpShortcutsTitle"));
        
        AddSubtitle(L.Get("HelpShortcutsGeneral"));
        AddShortcut("Ctrl+N", L.Get("ShortcutNewNote"));
        AddShortcut("Ctrl+S", L.Get("ShortcutSave"));
        AddShortcut("Ctrl+F", L.Get("ShortcutSearch"));
        AddShortcut("Ctrl+W", L.Get("ShortcutClose"));
        AddShortcut("Escape", L.Get("ShortcutHide"));
        
        AddSubtitle(L.Get("HelpShortcutsSnippets"));
        AddShortcut("Ctrl+Shift+S", L.Get("ShortcutSaveSnippet"));
        AddShortcut("Ctrl+Shift+I", L.Get("ShortcutInsertSnippet"));
        
        AddSubtitle(L.Get("HelpShortcutsFormat"));
        AddShortcut("Ctrl+Shift+F", L.Get("ShortcutFormat"));
        
        AddSubtitle(L.Get("HelpShortcutsHelp"));
        AddShortcut("F1", L.Get("ShortcutHelp"));
    }
    
    private void ShowTipsHelp()
    {
        ContentPanel.Children.Clear();
        AddTitle(L.Get("HelpTipsTitle"));
        
        AddTip("💡", L.Get("HelpTip1"));
        AddTip("💡", L.Get("HelpTip2"));
        AddTip("💡", L.Get("HelpTip3"));
        AddTip("💡", L.Get("HelpTip4"));
        AddTip("💡", L.Get("HelpTip5"));
        AddTip("💡", L.Get("HelpTip6"));
    }
    
    #endregion
    
    #region UI Helpers
    
    private void AddTitle(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        });
    }
    
    private void AddSubtitle(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("BlueBrush"),
            Margin = new Thickness(0, 16, 0, 8)
        });
    }
    
    private void AddParagraph(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }
    
    private void AddBullet(string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = "•",
            FontSize = 13,
            Foreground = (Brush)FindResource("SubtextBrush"),
            Margin = new Thickness(0, 0, 8, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap
        });
        ContentPanel.Children.Add(panel);
    }
    
    private void AddShortcut(string key, string description)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        
        var keyBorder = new Border
        {
            Background = (Brush)FindResource("Surface1Brush"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 12, 0)
        };
        keyBorder.Child = new TextBlock
        {
            Text = key,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)FindResource("GreenBrush")
        };
        panel.Children.Add(keyBorder);
        
        panel.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        ContentPanel.Children.Add(panel);
    }
    
    private void AddTip(string icon, string text)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Surface0Brush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 8, 0, 8)
        };
        
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 16,
            Margin = new Thickness(0, 0, 10, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 450
        });
        border.Child = panel;
        ContentPanel.Children.Add(border);
    }
    
    #endregion
}
