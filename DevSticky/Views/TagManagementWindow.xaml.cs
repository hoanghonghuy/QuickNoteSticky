using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

using TextBox = System.Windows.Controls.TextBox;

namespace DevSticky.Views;

public partial class TagManagementWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private TextBox? _currentEditBox;
    private Guid? _editingTagId;
    private Guid? _colorPickerTagId;

    public TagManagementWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        InitializeColorPalette();
        RefreshTagsList();
    }

    private void InitializeColorPalette()
    {
        ColorPalette.Children.Clear();
        foreach (var color in NoteTag.DefaultColors)
        {
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                Tag = color,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btn.Template = CreateColorButtonTemplate();
            btn.Click += ColorPalette_Click;
            ColorPalette.Children.Add(btn);
        }
    }

    private ControlTemplate CreateColorButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        border.Name = "bd";
        
        var trigger = new Trigger { Property = IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BorderBrushProperty, 
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4")), "bd"));
        template.Triggers.Add(trigger);
        
        template.VisualTree = border;
        return template;
    }

    public void RefreshTagsList()
    {
        var tags = _mainViewModel.Tags.Select(t => new TagListItem
        {
            Id = t.Id,
            Name = t.Name,
            Color = t.Color,
            NoteCount = _mainViewModel.Notes.Count(n => n.TagIds.Contains(t.Id))
        }).ToList();

        TagsList.ItemsSource = tags;
        TagCount.Text = L.Get("TagCount", tags.Count);
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

    private void BtnNewTag_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.CreateTag();
        RefreshTagsList();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid tagId)
        {
            var tag = _mainViewModel.Tags.FirstOrDefault(t => t.Id == tagId);
            var noteCount = _mainViewModel.Notes.Count(n => n.TagIds.Contains(tagId));

            var message = noteCount > 0
                ? L.Get("DeleteTagMessageWithNotes", tag?.Name ?? "", noteCount)
                : L.Get("DeleteTagMessage", tag?.Name ?? "");

            if (CustomDialog.ConfirmWarning(L.Get("DeleteTagTitle"), message, this))
            {
                _mainViewModel.DeleteTag(tagId);
                RefreshTagsList();
            }
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid tagId)
        {
            StartEditing(tagId);
        }
    }

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid tagId)
        {
            _colorPickerTagId = tagId;
            ColorPickerPopup.PlacementTarget = btn;
            ColorPickerPopup.IsOpen = true;
        }
    }

    private void ColorPalette_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color && _colorPickerTagId.HasValue)
        {
            _mainViewModel.ChangeTagColor(_colorPickerTagId.Value, color);
            ColorPickerPopup.IsOpen = false;
            _colorPickerTagId = null;
            RefreshTagsList();
        }
    }

    private void TagName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is StackPanel panel)
        {
            var item = FindParent<ListBoxItem>(panel);
            if (item?.DataContext is TagListItem tagItem)
            {
                StartEditing(tagItem.Id);
            }
        }
    }

    private void StartEditing(Guid tagId)
    {
        _editingTagId = tagId;

        foreach (var item in TagsList.Items)
        {
            var container = TagsList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (container != null)
            {
                var displayPanel = FindChild<StackPanel>(container, "DisplayPanel");
                var editBox = FindChild<TextBox>(container, "EditTextBox");

                if (item is TagListItem tagItem && tagItem.Id == tagId)
                {
                    if (displayPanel != null) displayPanel.Visibility = Visibility.Collapsed;
                    if (editBox != null)
                    {
                        editBox.Text = tagItem.Name;
                        editBox.Visibility = Visibility.Visible;
                        editBox.Focus();
                        editBox.SelectAll();
                        _currentEditBox = editBox;
                    }
                }
                else
                {
                    if (displayPanel != null) displayPanel.Visibility = Visibility.Visible;
                    if (editBox != null) editBox.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveEdit();
    }

    private void SaveEdit()
    {
        if (_editingTagId.HasValue && _currentEditBox != null)
        {
            var newName = _currentEditBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _mainViewModel.RenameTag(_editingTagId.Value, newName);
            }
        }
        _editingTagId = null;
        _currentEditBox = null;
        RefreshTagsList();
    }

    private void CancelEdit()
    {
        _editingTagId = null;
        _currentEditBox = null;
        RefreshTagsList();
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
                return element;
            var result = FindChild<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T result)
                return result;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}

public class TagListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#89B4FA";
    public int NoteCount { get; set; }
}
