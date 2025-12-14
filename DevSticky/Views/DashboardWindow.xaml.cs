using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Interfaces;
using DevSticky.Services;
using DevSticky.ViewModels;
using DevSticky.Models;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBox = System.Windows.Controls.TextBox;

namespace DevSticky.Views;

public partial class DashboardWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly IFuzzySearchService _fuzzySearchService;
    private readonly IFolderService _folderService;
    private readonly ISmartCollectionService _smartCollectionService;
    private ICloudConnection? _cloudConnection;
    private ICloudSync? _cloudSync;
    private Guid? _editingNoteId;
    private Guid? _filterTagId;
    private bool _isGroupedView;
    private bool _isFolderViewVisible;
    private bool _isSearchVisible;
    private string _searchQuery = string.Empty;
    private List<FuzzySearchResult>? _searchResults;
    
    public DashboardWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        _mainViewModel = mainViewModel;
        _fuzzySearchService = App.GetService<IFuzzySearchService>();
        _folderService = App.GetService<IFolderService>();
        _smartCollectionService = App.GetService<ISmartCollectionService>();
        
        // Try to get cloud sync service for status display
        try
        {
            _cloudConnection = App.GetService<ICloudConnection>();
            _cloudSync = App.GetService<ICloudSync>();
            if (_cloudSync != null)
            {
                _cloudSync.SyncProgress += OnSyncProgress;
            }
        }
        catch
        {
            // Cloud sync service not available
        }
        
        RefreshNotesList();
        LoadSmartCollections();
        UpdateCloudSyncStatus();
        UpdateClearButtonVisibility();
        
        // Subscribe to language changes to update UI text
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }
    
    /// <summary>
    /// Handle language changes and refresh UI text
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Refresh notes list to update all localized text (Ungrouped, EmptyNote, NoteCount, etc.)
        RefreshNotesList();
        
        // Update cloud sync status text
        UpdateCloudSyncStatus();
        
        // Update group view button tooltip
        BtnGroupView.ToolTip = _isGroupedView ? L.Get("SwitchToFlatView") : L.Get("SwitchToGroupedView");
    }

    public void RefreshNotesList()
    {
        var groupOptions = new List<GroupOption> { new() { Id = null, Name = L.Get("Ungrouped") } };
        groupOptions.AddRange(_mainViewModel.Groups.Select(g => new GroupOption { Id = g.Id, Name = g.Name }));
        
        var notesQuery = _mainViewModel.Notes.AsEnumerable();
        
        // Apply search filter if active
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var notes = _mainViewModel.Notes.Select(vm => new Note 
            { 
                Id = vm.Id, 
                Title = vm.Title, 
                Content = vm.Content,
                CreatedDate = vm.CreatedDate,
                ModifiedDate = vm.ModifiedDate,
                TagIds = vm.TagIds,
                GroupId = vm.GroupId
            }).ToList();
            
            _searchResults = _fuzzySearchService.Search(notes, _searchQuery).ToList();
            notesQuery = _searchResults.Select(sr => _mainViewModel.Notes.First(vm => vm.Id == sr.Note.Id));
        }
        
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
        
        if (_isGroupedView && string.IsNullOrWhiteSpace(_searchQuery))
        {
            // Group notes by their group (only when not searching)
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
            // Flat view or search results
            items.AddRange(notesQuery.Select(n => CreateNoteListItem(n, groupOptions)));
        }
        
        NotesList.ItemsSource = items;
        var totalNotes = notesQuery.Count();
        
        if (!string.IsNullOrWhiteSpace(_searchQuery))
            NoteCount.Text = $"{totalNotes} result(s)"; // Short format - search query already visible in search box
        else if (_filterTagId.HasValue && _isGroupedView)
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
            CreatedDate = n.CreatedDate.ToString("MMM dd, HH:mm", LocalizationService.Instance.CurrentCulture),
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

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        _isSearchVisible = !_isSearchVisible;
        SearchBar.Visibility = _isSearchVisible ? Visibility.Visible : Visibility.Collapsed;
        
        if (_isSearchVisible)
        {
            // Delay focus to ensure TextBox is fully rendered
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchBox.Focus();
                SearchBox.CaretIndex = SearchBox.Text.Length; // Set cursor to end
                UpdatePlaceholderVisibility();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        else
        {
            // Clear search when hiding
            SearchBox.Text = string.Empty;
            UpdatePlaceholderVisibility();
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholderVisibility();
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholderVisibility();
    }

    private void UpdatePlaceholderVisibility()
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
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

    private void BtnOverflowMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // Prevent event from bubbling up to ListBoxItem
        
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
            System.Diagnostics.Debug.WriteLine($"Failed to open template management: {ex.Message}\n{ex.StackTrace}");
            CustomDialog.ShowInfo(L.Get("Error"), $"{L.Get("TemplateManagementNotAvailable")}\n{ex.Message}", this);
        }
    }

    private void BtnGraphView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new GraphViewWindow();
            window.NoteClicked += (s, noteId) => _mainViewModel.OpenNoteById(noteId);
            window.Owner = this;
            window.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open graph view: {ex.Message}\n{ex.StackTrace}");
            CustomDialog.ShowInfo(L.Get("Error"), $"{L.Get("GraphViewNotAvailable")}\n{ex.Message}", this);
        }
    }

    private void BtnKanbanView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new KanbanWindow();
            window.Owner = this;
            window.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open Kanban view: {ex.Message}\n{ex.StackTrace}");
            CustomDialog.ShowInfo(L.Get("Error"), $"{L.Get("KanbanViewNotAvailable")}\n{ex.Message}", this);
        }
    }

    private void BtnTimelineView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new TimelineWindow();
            window.Owner = this;
            window.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open Timeline view: {ex.Message}\n{ex.StackTrace}");
            CustomDialog.ShowInfo(L.Get("Error"), $"{L.Get("TimelineViewNotAvailable")}\n{ex.Message}", this);
        }
    }

    // Cloud Sync Status
    private void UpdateCloudSyncStatus()
    {
        if (_cloudConnection == null)
        {
            SyncStatusBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SyncStatusBadge.Visibility = Visibility.Visible;
        
        switch (_cloudConnection?.Status ?? SyncStatus.Disconnected)
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
                var lastSync = _cloudSync?.LastSyncResult?.CompletedAt;
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
        if (_cloudSync == null || _cloudConnection == null) return;

        if (_cloudConnection.Status == SyncStatus.Error || _cloudConnection.Status == SyncStatus.Idle)
        {
            try
            {
                await _cloudSync.SyncAsync();
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
        // Just hide the window instead of closing
        // Keep event subscriptions active so language changes still work
        e.Cancel = true;
        Hide();
    }
    
    /// <summary>
    /// Call this when the application is actually shutting down to clean up resources
    /// </summary>
    public void Shutdown()
    {
        // Unsubscribe from cloud sync events
        if (_cloudSync != null)
        {
            _cloudSync.SyncProgress -= OnSyncProgress;
        }
        
        // Unsubscribe from language change events
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
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

    // Search functionality
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _searchQuery = textBox.Text?.Trim() ?? string.Empty;
            UpdateClearButtonVisibility();
            UpdatePlaceholderVisibility();
            RefreshNotesList();
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClearSearch();
            e.Handled = true;
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
    }

    private void ClearSearch()
    {
        SearchBox.Text = string.Empty;
        _searchQuery = string.Empty;
        _searchResults = null;
        UpdateClearButtonVisibility();
        RefreshNotesList();
        SearchBox.Focus();
    }

    private void UpdateClearButtonVisibility()
    {
        ClearSearchButton.Visibility = HasSearchQuery ? Visibility.Visible : Visibility.Collapsed;
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value ?? string.Empty;
            UpdateClearButtonVisibility();
            RefreshNotesList();
        }
    }

    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(_searchQuery);

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from cloud sync events
        if (_cloudSync != null)
        {
            _cloudSync.SyncProgress -= OnSyncProgress;
        }

        base.OnClosed(e);
    }

    #region Folder Management

    private async void BtnToggleFolderView_Click(object sender, RoutedEventArgs e)
    {
        _isFolderViewVisible = !_isFolderViewVisible;
        FoldersSection.Visibility = _isFolderViewVisible ? Visibility.Visible : Visibility.Collapsed;
        
        if (_isFolderViewVisible)
        {
            await LoadFoldersAsync();
        }
    }

    private async Task LoadFoldersAsync()
    {
        try
        {
            await _folderService.LoadAsync();
            var rootFolders = await _folderService.GetRootFoldersAsync();
            var folderViewModels = new List<FolderViewModel>();
            
            foreach (var folder in rootFolders)
            {
                var folderVm = await CreateFolderViewModelAsync(folder);
                folderViewModels.Add(folderVm);
            }
            
            FoldersTreeView.ItemsSource = folderViewModels;
        }
        catch (Exception ex)
        {
            // Log error but don't crash the UI
            System.Diagnostics.Debug.WriteLine($"Error loading folders: {ex.Message}");
        }
    }

    private async Task<FolderViewModel> CreateFolderViewModelAsync(NoteFolder folder)
    {
        var notesInFolder = await _folderService.GetNotesInFolderAsync(folder.Id);
        var childFolders = await _folderService.GetChildFoldersAsync(folder.Id);
        
        var folderVm = new FolderViewModel
        {
            Id = folder.Id,
            Name = folder.Name,
            NoteCount = notesInFolder.Count,
            Children = new List<FolderViewModel>()
        };
        
        foreach (var childFolder in childFolders)
        {
            var childVm = await CreateFolderViewModelAsync(childFolder);
            folderVm.Children.Add(childVm);
        }
        
        return folderVm;
    }

    private async void FolderTreeItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is TreeViewItem treeViewItem && 
            treeViewItem.DataContext is FolderViewModel folderVm &&
            e.Data.GetData(typeof(Guid)) is Guid noteId)
        {
            await _folderService.MoveNoteToFolderAsync(noteId, folderVm.Id);
            RefreshNotesList();
            await LoadFoldersAsync(); // Refresh folder counts
        }
    }

    private void FolderTreeItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(Guid)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FolderItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is StackPanel panel && panel.DataContext is FolderViewModel folderVm)
        {
            var contextMenu = new ContextMenu();
            
            var newFolderItem = new MenuItem { Header = "New Subfolder" };
            newFolderItem.Click += async (s, args) => await CreateSubfolderAsync(folderVm.Id);
            contextMenu.Items.Add(newFolderItem);
            
            var renameFolderItem = new MenuItem { Header = "Rename" };
            renameFolderItem.Click += (s, args) => RenameFolderAsync(folderVm);
            contextMenu.Items.Add(renameFolderItem);
            
            contextMenu.Items.Add(new Separator());
            
            var deleteFolderItem = new MenuItem { Header = "Delete" };
            deleteFolderItem.Click += async (s, args) => await DeleteFolderAsync(folderVm.Id);
            contextMenu.Items.Add(deleteFolderItem);
            
            panel.ContextMenu = contextMenu;
        }
    }

    private async Task CreateSubfolderAsync(Guid parentId)
    {
        // Simple prompt for folder name - in a real implementation, you'd use a proper dialog
        var folderName = "New Folder"; // Default name for now
        
        try
        {
            await _folderService.CreateFolderAsync(folderName, parentId);
            await LoadFoldersAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error creating folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RenameFolderAsync(FolderViewModel folderVm)
    {
        // Note: This would require adding a RenameFolder method to IFolderService
        // For now, we'll skip this functionality
        System.Windows.MessageBox.Show("Rename functionality not yet implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task DeleteFolderAsync(Guid folderId)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to delete this folder? Notes will be moved to the parent folder.",
            "Delete Folder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            await _folderService.DeleteFolderAsync(folderId);
            RefreshNotesList();
            await LoadFoldersAsync();
        }
    }

    #endregion

    #region Smart Collections

    private async void LoadSmartCollections()
    {
        try
        {
            var defaultCollections = _smartCollectionService.GetDefaultCollections();
            var collectionsWithCounts = new List<SmartCollectionDisplay>();

            foreach (var collection in defaultCollections)
            {
                var notes = await _smartCollectionService.GetNotesForCollectionAsync(collection.Id);
                collectionsWithCounts.Add(new SmartCollectionDisplay
                {
                    Id = collection.Id,
                    Name = collection.Name,
                    Icon = collection.Icon,
                    NoteCount = notes.Count
                });
            }

            SmartCollectionsList.ItemsSource = collectionsWithCounts;
        }
        catch (Exception ex)
        {
            // Log error but don't crash the UI
            System.Diagnostics.Debug.WriteLine($"Error loading smart collections: {ex.Message}");
        }
    }

    private async void SmartCollectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is SmartCollectionDisplay collection)
        {
            try
            {
                var notes = await _smartCollectionService.GetNotesForCollectionAsync(collection.Id);
                var noteViewModels = notes.Select(n => _mainViewModel.Notes.FirstOrDefault(vm => vm.Id == n.Id))
                                         .Where(vm => vm != null)
                                         .ToList();

                // Update the notes list to show only the filtered notes
                var groupOptions = new List<GroupOption> { new() { Id = null, Name = L.Get("Ungrouped") } };
                groupOptions.AddRange(_mainViewModel.Groups.Select(g => new GroupOption { Id = g.Id, Name = g.Name }));

                var items = noteViewModels.Select(n => CreateNoteListItem(n!, groupOptions)).Cast<object>().ToList();
                NotesList.ItemsSource = items;

                // Update note count display
                NoteCount.Text = $"{collection.Icon} {collection.Name}: {notes.Count} notes";

                // Clear other filters
                _filterTagId = null;
                _searchQuery = string.Empty;
                SearchBox.Text = string.Empty;
                FilterBadge.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error filtering by smart collection: {ex.Message}");
            }
        }
    }

    #endregion
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

public class FolderViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int NoteCount { get; set; }
    public List<FolderViewModel> Children { get; set; } = new();
}

public class SmartCollectionDisplay
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Icon { get; set; }
    public int NoteCount { get; set; }
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
