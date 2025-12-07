using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Services;
using DevSticky.ViewModels;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;

namespace DevSticky.Views;

public partial class GroupManagementWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private TextBox? _currentEditBox;
    private Guid? _editingGroupId;

    public GroupManagementWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        RefreshGroupsList();
    }

    public void RefreshGroupsList()
    {
        var groups = _mainViewModel.Groups.Select(g => new GroupListItem
        {
            Id = g.Id,
            Name = g.Name,
            NoteCount = _mainViewModel.Notes.Count(n => n.GroupId == g.Id)
        }).ToList();

        GroupsList.ItemsSource = groups;
        GroupCount.Text = L.Get("GroupCount", groups.Count);
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

    private void BtnNewGroup_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.CreateGroup();
        RefreshGroupsList();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid groupId)
        {
            var group = _mainViewModel.Groups.FirstOrDefault(g => g.Id == groupId);
            var noteCount = _mainViewModel.Notes.Count(n => n.GroupId == groupId);
            
            var message = noteCount > 0
                ? L.Get("DeleteGroupMessageWithNotes", group?.Name ?? "", noteCount)
                : L.Get("DeleteGroupMessage", group?.Name ?? "");

            if (CustomDialog.ConfirmWarning(L.Get("DeleteGroupTitle"), message, this))
            {
                _mainViewModel.DeleteGroup(groupId);
                RefreshGroupsList();
            }
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid groupId)
        {
            StartEditing(groupId);
        }
    }

    private void GroupName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is StackPanel panel)
        {
            var item = FindParent<ListBoxItem>(panel);
            if (item?.DataContext is GroupListItem groupItem)
            {
                StartEditing(groupItem.Id);
            }
        }
    }

    private void StartEditing(Guid groupId)
    {
        _editingGroupId = groupId;
        
        foreach (var item in GroupsList.Items)
        {
            var container = GroupsList.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
            if (container != null)
            {
                var displayPanel = FindChild<StackPanel>(container, "DisplayPanel");
                var editBox = FindChild<TextBox>(container, "EditTextBox");
                
                if (item is GroupListItem groupItem && groupItem.Id == groupId)
                {
                    if (displayPanel != null) displayPanel.Visibility = Visibility.Collapsed;
                    if (editBox != null)
                    {
                        editBox.Text = groupItem.Name;
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
        if (_editingGroupId.HasValue && _currentEditBox != null)
        {
            var newName = _currentEditBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _mainViewModel.RenameGroup(_editingGroupId.Value, newName);
            }
        }
        _editingGroupId = null;
        _currentEditBox = null;
        RefreshGroupsList();
    }

    private void CancelEdit()
    {
        _editingGroupId = null;
        _currentEditBox = null;
        RefreshGroupsList();
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

public class GroupListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int NoteCount { get; set; }
}
