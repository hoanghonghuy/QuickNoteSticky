using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Interfaces;
using DevSticky.Services;
using DevSticky.ViewModels;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace DevSticky.Views;

public partial class DashboardWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private ICloudSyncService? _cloudSyncService;
    private Guid? _editingNoteId;
    private Guid? _filterTagId;
    private bool _isGroupedView;
    
    public DashboardWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        
        // Try to get cloud sync service for status display
        try
        {
            _cloudSyncService = App.GetService<ICloudSyncService>();
            if (_cloudSyncService != null)
            {
                _cloudSyncService.SyncProgress += OnSyncProgress;
            }
        }
        catch
        {
            // Cloud sync service not available
        }
        
        RefreshNotesList();
        UpdateCloudSyncStatus();
    }

    public void RefreshNotesList()
    {
        var groupOptions = new List<GroupOption> { new() { Id = null, Name = L.Get("Ungrouped") } };
        groupOptions.AddRange(_mainViewModel.Groups.Select(g => new GroupOption { Id = g.Id, Name = g.Name }));
        
        var notesQuery = _mainViewModel.Notes.AsEnumerable();
        
        // Apply tag filter if active
        if (_filterTagId.HasValue)
        {
            notesQuery = notesQuery.Where(n => n.TagIds.Contains(_filterTagId.Value));
            var filterTag = _mainViewModel.Tags.FirstOrDefault(t => t.Id == _filterTagId.Value);
            FilterText.Text = $"🏷️ {filterTag?.Name}";
            FilterBadge.Visibility = Visibility.Visible;
        }
        else
        {
            FilterBadge.Visibility = Visibility.Collapsed;
        }

        var items = new List<object>();
        
        if (_isGroupedView)
        {
            // Group notes by their group
            var ungroupedNotes = notesQuery.Where(n => n.GroupId == null).ToList();
            var groupedNotes = notesQuery.Where(n => n.GroupId != null)
                .GroupBy(n => n.GroupId)
                .ToList();

            // Add grouped notes first
            foreach (var group in _mainViewModel.Groups)
            {
                var notesInGroup = groupedNotes.FirstOrDefault(g => g.Key == group.Id)?.ToList() ?? new List<NoteViewModel>();
                items.Add(new GroupHeader 
                { 
                    Id = group.Id, 
                    Name = group.Name, 
                    NoteCount = notesInGroup.Count,
                    IsExpanded = group.IsExpanded
                });
                
                if (group.IsExpanded)
                {
                    items.AddRange(notesInGroup.Select(n => CreateNoteListItem(n, groupOptions)));
                }
            }

            // Add ungrouped notes at the end
            if (ungroupedNotes.Any())
            {
                items.Add(new GroupHeader { Id = null, Name = L.Get("Ungrouped"), NoteCount = ungroupedNotes.Count, IsExpanded = true });
                items.AddRange(ungroupedNotes.Select(n => CreateNoteListItem(n, groupOptions)));
            }
        }
        else
        {
            items.AddRange(notesQuery.Select(n => CreateNoteListItem(n, groupOptions)));
        }
        
        NotesList.ItemsSource = items;
        var totalNotes = _mainViewModel.Notes.Count(n => !_filterTagId.HasValue || n.TagIds.Contains(_filterTagId.Value));
        
        if (_filterTagId.HasValue && _isGroupedView)
            NoteCount.Text = L.Get("NoteCountFiltered", totalNotes) + " • " + L.Get("NoteCountGrouped", totalNotes).Split('•').Last().Trim();
        else if (_filterTagId.HasValue)
            NoteCount.Text = L.Get("NoteCountFiltered", totalNotes);
        else if (_isGroupedView)
            NoteCount.Text = L.Get("NoteCountGrouped", totalNotes);
        else
            NoteCount.Text = L.Get("NoteCount", totalNotes);
    }

    private NoteListItem CreateNoteListItem(NoteViewModel n, List<GroupOption> groupOptions)
    {
        return new NoteListItem
        {
            Id = n.Id,
            Title = n.Title,
            Preview = string.IsNullOrWhiteSpace(n.Content) 
                ? L.Get("EmptyNote") 
                : (n.Content.Length > 50 ? n.Content[..50] + "..." : n.Content).Replace("\n", " ").Replace("\r", ""),
            Language = n.Language,
            CreatedDate = n.CreatedDate.ToString("MMM dd, HH:mm"),
            PinStatus = n.IsPinned ? "●" : "",
            GroupId = n.GroupId,
            GroupOptions = groupOptions,
            Tags = GetTagsForNote(n.Id, n.TagIds)
        };
    }

    private List<TagDisplay> GetTagsForNote(Guid noteId, List<Guid> tagIds)
    {
        // Use cache for better performance
        var tags = _mainViewModel.Cache.GetTags(tagIds, _mainViewModel.Tags);
        return tags.Select(t => new TagDisplay 
        { 
            Id = t.Id, 
            Name = t.Name, 
            Color = t.Color, 
            NoteId = noteId 
        }).ToList();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_mainViewModel.AppSettings);
        settingsWindow.ShowDialog();
    }

    private void BtnNewNote_Click(object sender, RoutedEventArgs e)
    {
        _mainViewModel.CreateNewNote();
        RefreshNotesList();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid noteId)
        {
            if (CustomDialog.ConfirmWarning(L.Get("DeleteNoteTitle"), L.Get("DeleteNoteMessage"), this))
            {
                var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == noteId);
                if (noteVm != null)
                {
                    _mainViewModel.RemoveNote(noteVm);
                    RefreshNotesList();
                }
            }
        }
    }

    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NotesList.SelectedItem is NoteListItem item && _editingNoteId == null)
        {
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == item.Id);
            if (noteVm != null)
            {
                _mainViewModel.ShowNote(noteVm);
            }
            NotesList.SelectedItem = null;
        }
    }

    // Manage Groups/Tags buttons
    private void BtnManageGroups_Click(object sender, RoutedEventArgs e)
    {
        var window = new GroupManagementWindow(_mainViewModel);
        window.ShowDialog();
        RefreshNotesList();
    }

    private void BtnManageTags_Click(object sender, RoutedEventArgs e)
    {
        var window = new TagManagementWindow(_mainViewModel);
        window.ShowDialog();
        RefreshNotesList();
    }

    // New v2.0 feature buttons
    private void BtnSnippetLibrary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new SnippetBrowserWindow();
            window.Owner = this;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open snippet library: {ex.Message}");
            CustomDialog.ShowInfo(L.Get("Error"), L.Get("SnippetLibraryNotAvailable"), this);
        }
    }

    private void BtnTemplateManagement_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new TemplateSelectionDialog();
            window.Owner = this;
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open template management: {ex.Message}");
            CustomDialog.ShowInfo(L.Get("Error"), L.Get("TemplateManagementNotAvailable"), this);
        }
    }

    private void BtnGraphView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new GraphViewWindow();
            window.NoteClicked += (_, noteId) => _mainViewModel.OpenNoteById(noteId);
            window.Owner = this;
            window.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open graph view: {ex.Message}");
            CustomDialog.ShowInfo(L.Get("Error"), L.Get("GraphViewNotAvailable"), this);
        }
    }

    // Cloud Sync Status
    private void UpdateCloudSyncStatus()
    {
        if (_cloudSyncService == null)
        {
            SyncStatusBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SyncStatusBadge.Visibility = Visibility.Visible;
        
        switch (_cloudSyncService.Status)
        {
            case SyncStatus.Disconnected:
                SyncStatusIcon.Text = "☁️";
                SyncStatusText.Text = L.Get("CloudStatusDisconnected");
                SyncStatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#45475A"));
                break;
            case SyncStatus.Connecting:
                SyncStatusIcon.Text = "🔄";
                SyncStatusText.Text = L.Get("CloudStatusConnecting");
                SyncStatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F9E2AF"));
                break;
            case SyncStatus.Syncing:
                SyncStatusIcon.Text = "🔄";
                SyncStatusText.Text = L.Get("CloudStatusSyncing");
                SyncStatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89B4FA"));
                break;
            case SyncStatus.Idle:
                SyncStatusIcon.Text = "✓";
                var lastSync = _cloudSyncService.LastSyncResult?.CompletedAt;
                SyncStatusText.Text = lastSync.HasValue 
                    ? L.Get("CloudStatusConnectedLastSync", lastSync.Value.ToString("HH:mm"))
                    : L.Get("CloudStatusConnected");
                SyncStatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A6E3A1"));
                break;
            case SyncStatus.Error:
                SyncStatusIcon.Text = "⚠️";
                SyncStatusText.Text = L.Get("CloudStatusError");
                SyncStatusBadge.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F38BA8"));
                break;
        }
    }

    private void OnSyncProgress(object? sender, SyncProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateCloudSyncStatus();
        });
    }

    private async void SyncStatus_Click(object sender, MouseButtonEventArgs e)
    {
        if (_cloudSyncService == null) return;

        if (_cloudSyncService.Status == SyncStatus.Error || _cloudSyncService.Status == SyncStatus.Idle)
        {
            try
            {
                await _cloudSyncService.SyncAsync();
                UpdateCloudSyncStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Manual sync failed: {ex.Message}");
            }
        }
    }

    // Inline Title Edit
    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is TextBlock textBlock)
        {
            var item = FindParent<ListBoxItem>(textBlock);
            if (item?.DataContext is NoteListItem noteItem)
            {
                _editingNoteId = noteItem.Id;
                var titleEdit = FindChild<TextBox>(item, "TitleEdit");
                var titleDisplay = FindChild<TextBlock>(item, "TitleDisplay");
                
                if (titleEdit != null && titleDisplay != null)
                {
                    titleDisplay.Visibility = Visibility.Collapsed;
                    titleEdit.Text = noteItem.Title ?? "";
                    titleEdit.Visibility = Visibility.Visible;
                    titleEdit.Focus();
                    titleEdit.SelectAll();
                }
            }
            e.Handled = true;
        }
    }

    private void TitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveTitleEdit(sender as TextBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTitleEdit(sender as TextBox);
            e.Handled = true;
        }
    }

    private void TitleEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveTitleEdit(sender as TextBox);
    }

    private void SaveTitleEdit(TextBox? textBox)
    {
        if (_editingNoteId.HasValue && textBox != null)
        {
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == _editingNoteId.Value);
            if (noteVm != null)
            {
                var newTitle = textBox.Text.Trim();
                noteVm.Title = string.IsNullOrEmpty(newTitle) ? L.Get("UntitledNote") : newTitle;
                _mainViewModel.SaveAllNotes();
            }
        }
        _editingNoteId = null;
        RefreshNotesList();
    }

    private void CancelTitleEdit(TextBox? textBox)
    {
        _editingNoteId = null;
        RefreshNotesList();
    }

    // Group Selector
    private void GroupSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.Tag is Guid noteId)
        {
            var groupId = combo.SelectedValue as Guid?;
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == noteId);
            if (noteVm != null)
            {
                _mainViewModel.MoveNoteToGroup(noteVm, groupId);
            }
        }
    }

    // Tag Management
    private void BtnAddTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid noteId)
        {
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == noteId);
            if (noteVm == null || noteVm.TagIds.Count >= 5) return;

            var availableTags = _mainViewModel.Tags.Where(t => !noteVm.TagIds.Contains(t.Id)).ToList();
            if (availableTags.Count == 0)
            {
                CustomDialog.ShowInfo(L.Get("Info"), L.Get("NoTagsAvailable"), this);
                return;
            }

            var menu = new ContextMenu();
            foreach (var tag in availableTags)
            {
                var item = new MenuItem
                {
                    Header = tag.Name,
                    Tag = new Tuple<Guid, Guid>(noteId, tag.Id),
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tag.Color))
                };
                item.Click += TagMenuItem_Click;
                menu.Items.Add(item);
            }
            menu.PlacementTarget = btn;
            menu.IsOpen = true;
        }
    }

    private void TagMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is Tuple<Guid, Guid> ids)
        {
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == ids.Item1);
            if (noteVm != null)
            {
                _mainViewModel.AddTagToNote(noteVm, ids.Item2);
                RefreshNotesList();
            }
        }
    }

    private void BtnRemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TagDisplay tagDisplay)
        {
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == tagDisplay.NoteId);
            if (noteVm != null)
            {
                _mainViewModel.RemoveTagFromNote(noteVm, tagDisplay.Id);
                RefreshNotesList();
            }
        }
        e.Handled = true;
    }

    // Tag Filter
    private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _filterTagId = null;
        RefreshNotesList();
    }

    public void FilterByTag(Guid tagId)
    {
        _filterTagId = tagId;
        RefreshNotesList();
    }

    private void TagBadge_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.Tag is Guid tagId)
        {
            FilterByTag(tagId);
            e.Handled = true;
        }
    }

    // Grouped View Toggle
    private void BtnToggleGroupView_Click(object sender, RoutedEventArgs e)
    {
        _isGroupedView = !_isGroupedView;
        BtnGroupView.Content = _isGroupedView ? "📋" : "📂";
        BtnGroupView.ToolTip = _isGroupedView ? L.Get("SwitchToFlatView") : L.Get("SwitchToGroupedView");
        RefreshNotesList();
    }

    private void GroupHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.Tag is Guid groupId)
        {
            var group = _mainViewModel.Groups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
                group.IsExpanded = !group.IsExpanded;
                _mainViewModel.SaveAllNotes();
                RefreshNotesList();
            }
        }
        e.Handled = true;
    }

    // Drag-Drop for notes to groups
    private bool _isDragging;

    private void NoteItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
        {
            var currentPos = e.GetPosition(null);
            if (sender is Grid grid && grid.Tag is Guid noteId)
            {
                _isDragging = true;
                var data = new DataObject("NoteId", noteId);
                DragDrop.DoDragDrop(grid, data, DragDropEffects.Move);
                _isDragging = false;
            }
        }
    }

    private void GroupHeader_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("NoteId"))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void GroupHeader_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("NoteId") && sender is System.Windows.Controls.Border border)
        {
            var noteId = (Guid)e.Data.GetData("NoteId");
            var groupId = border.Tag as Guid?;
            
            var noteVm = _mainViewModel.Notes.FirstOrDefault(n => n.Id == noteId);
            if (noteVm != null)
            {
                _mainViewModel.MoveNoteToGroup(noteVm, groupId);
                RefreshNotesList();
            }
        }
        e.Handled = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Unsubscribe from cloud sync events
        if (_cloudSyncService != null)
        {
            _cloudSyncService.SyncProgress -= OnSyncProgress;
        }
        
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// Refresh the cloud sync status display
    /// </summary>
    public void RefreshSyncStatus()
    {
        UpdateCloudSyncStatus();
    }

    // Helper methods
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

public class NoteListItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Preview { get; set; } = "";
    public string Language { get; set; } = "";
    public string CreatedDate { get; set; } = "";
    public string PinStatus { get; set; } = "";
    public Guid? GroupId { get; set; }
    public List<GroupOption> GroupOptions { get; set; } = new();
    public List<TagDisplay> Tags { get; set; } = new();
    
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) || Title == L.Get("UntitledNote") ? Preview : Title;
}

public class GroupOption
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
}

public class TagDisplay
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#89B4FA";
}

public class GroupHeader
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public int NoteCount { get; set; }
    public bool IsExpanded { get; set; } = true;
}

public class DashboardTemplateSelector : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (container is FrameworkElement element)
        {
            if (item is GroupHeader)
                return element.FindResource("GroupHeaderTemplate") as DataTemplate ?? base.SelectTemplate(item, container);
            if (item is NoteListItem)
                return element.FindResource("NoteItemTemplate") as DataTemplate ?? base.SelectTemplate(item, container);
        }
        return base.SelectTemplate(item, container);
    }
}
