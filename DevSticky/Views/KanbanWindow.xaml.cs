using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Services;
using DevSticky.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DevSticky.Views;

/// <summary>
/// Kanban board window for managing notes by status
/// </summary>
public partial class KanbanWindow : Window
{
    private readonly IKanbanService? _kanbanService;
    private readonly INoteService? _noteService;
    private readonly ITagManagementService? _tagService;
    
    // Collections for each column
    public ObservableCollection<KanbanCardViewModel> ToDoNotes { get; } = new();
    public ObservableCollection<KanbanCardViewModel> InProgressNotes { get; } = new();
    public ObservableCollection<KanbanCardViewModel> DoneNotes { get; } = new();
    
    private KanbanCardViewModel? _draggedCard;
    
    public KanbanWindow()
    {
        InitializeComponent();
        
        try
        {
            _kanbanService = App.ServiceProvider.GetRequiredService<IKanbanService>();
            _noteService = App.ServiceProvider.GetRequiredService<INoteService>();
            var mainViewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
            _tagService = mainViewModel.TagManagementService;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KanbanWindow] Failed to resolve services: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to initialize Kanban: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }
        
        // Set data context for binding
        ToDoItems.ItemsSource = ToDoNotes;
        InProgressItems.ItemsSource = InProgressNotes;
        DoneItems.ItemsSource = DoneNotes;
        
        Loaded += KanbanWindow_Loaded;
    }
    
    private async void KanbanWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadKanbanDataAsync();
    }
    
    /// <summary>
    /// Load all Kanban notes and populate columns (Requirements 5.1, 5.5)
    /// </summary>
    private async Task LoadKanbanDataAsync()
    {
        if (_kanbanService == null || _tagService == null)
        {
            System.Diagnostics.Debug.WriteLine("[KanbanWindow] Services not available");
            return;
        }
        
        try
        {
            var kanbanNotes = await _kanbanService.GetAllKanbanNotesAsync();
            var allTags = _tagService.GetAllTags();
            
            // Clear existing collections
            ToDoNotes.Clear();
            InProgressNotes.Clear();
            DoneNotes.Clear();
            
            // Populate each column
            foreach (var note in kanbanNotes[KanbanStatus.ToDo])
            {
                ToDoNotes.Add(CreateCardViewModel(note, allTags));
            }
            
            foreach (var note in kanbanNotes[KanbanStatus.InProgress])
            {
                InProgressNotes.Add(CreateCardViewModel(note, allTags));
            }
            
            foreach (var note in kanbanNotes[KanbanStatus.Done])
            {
                DoneNotes.Add(CreateCardViewModel(note, allTags));
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading Kanban data: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// Create a card view model from a note (Requirements 5.5)
    /// </summary>
    private KanbanCardViewModel CreateCardViewModel(Note note, IReadOnlyList<NoteTag> allTags)
    {
        // Get content preview (first 100 characters)
        var contentPreview = note.Content.Length > 100 
            ? note.Content.Substring(0, 100) + "..." 
            : note.Content;
        
        // Get tag names for this note
        var tagNames = note.TagIds
            .Select(tagId => allTags.FirstOrDefault(t => t.Id == tagId)?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
        
        return new KanbanCardViewModel
        {
            NoteId = note.Id,
            Title = note.Title,
            ContentPreview = contentPreview,
            Tags = tagNames!,
            Status = note.KanbanStatus ?? KanbanStatus.ToDo
        };
    }
    
    /// <summary>
    /// Handle card click to open note (Requirements 5.4)
    /// </summary>
    private async void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is KanbanCardViewModel card)
        {
            if (e.ClickCount == 2) // Double-click to open note
            {
                try
                {
                    if (_noteService == null) return;
                    var note = _noteService.GetAllNotes().FirstOrDefault(n => n.Id == card.NoteId);
                    if (note != null)
                    {
                        // Open the note window
                        var windowService = App.ServiceProvider.GetRequiredService<IWindowService>();
                        windowService.ShowNote(note);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error opening note: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (e.ClickCount == 1) // Single-click to start drag
            {
                _draggedCard = card;
                DragDrop.DoDragDrop(border, card, System.Windows.DragDropEffects.Move);
            }
        }
    }
    
    /// <summary>
    /// Handle card drop to change status (Requirements 5.2, 5.3)
    /// </summary>
    private async void Card_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_draggedCard != null && sender is Border border && _kanbanService != null)
        {
            // Determine target status based on which column the card was dropped in
            KanbanStatus targetStatus = DetermineTargetStatus(border);
            
            if (targetStatus != _draggedCard.Status)
            {
                try
                {
                    // Update note status in service
                    var success = await _kanbanService.UpdateNoteStatusAsync(_draggedCard.NoteId, targetStatus);
                    
                    if (success)
                    {
                        // Update UI by moving card between collections
                        RemoveCardFromCurrentColumn(_draggedCard);
                        _draggedCard.Status = targetStatus;
                        AddCardToTargetColumn(_draggedCard, targetStatus);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Failed to update note status", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error updating note status: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            
            _draggedCard = null;
        }
    }
    
    /// <summary>
    /// Determine target status based on drop location
    /// </summary>
    private KanbanStatus DetermineTargetStatus(Border dropTarget)
    {
        // Walk up the visual tree to find the parent column
        var parent = dropTarget.Parent;
        while (parent != null)
        {
            if (parent is Grid grid && grid.Parent is Border columnBorder)
            {
                var columnIndex = Grid.GetColumn(columnBorder);
                return columnIndex switch
                {
                    0 => KanbanStatus.ToDo,
                    1 => KanbanStatus.InProgress,
                    2 => KanbanStatus.Done,
                    _ => KanbanStatus.ToDo
                };
            }
            parent = LogicalTreeHelper.GetParent(parent);
        }
        
        return KanbanStatus.ToDo; // Default fallback
    }
    
    /// <summary>
    /// Remove card from its current column
    /// </summary>
    private void RemoveCardFromCurrentColumn(KanbanCardViewModel card)
    {
        switch (card.Status)
        {
            case KanbanStatus.ToDo:
                ToDoNotes.Remove(card);
                break;
            case KanbanStatus.InProgress:
                InProgressNotes.Remove(card);
                break;
            case KanbanStatus.Done:
                DoneNotes.Remove(card);
                break;
        }
    }
    
    /// <summary>
    /// Add card to target column
    /// </summary>
    private void AddCardToTargetColumn(KanbanCardViewModel card, KanbanStatus targetStatus)
    {
        switch (targetStatus)
        {
            case KanbanStatus.ToDo:
                ToDoNotes.Add(card);
                break;
            case KanbanStatus.InProgress:
                InProgressNotes.Add(card);
                break;
            case KanbanStatus.Done:
                DoneNotes.Add(card);
                break;
        }
    }
    
    private void Card_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 0.7;
        }
    }
    
    private void Card_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 1.0;
        }
    }
    
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
    
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadKanbanDataAsync();
    }
    
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    /// <summary>
    /// Handle drop on column (for dropping into empty areas or between cards)
    /// </summary>
    private async void Column_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_draggedCard == null || _kanbanService == null) return;
        
        if (sender is Border columnBorder && columnBorder.Tag is string statusTag)
        {
            var targetStatus = statusTag switch
            {
                "ToDo" => KanbanStatus.ToDo,
                "InProgress" => KanbanStatus.InProgress,
                "Done" => KanbanStatus.Done,
                _ => KanbanStatus.ToDo
            };
            
            if (targetStatus != _draggedCard.Status)
            {
                try
                {
                    var success = await _kanbanService.UpdateNoteStatusAsync(_draggedCard.NoteId, targetStatus);
                    
                    if (success)
                    {
                        RemoveCardFromCurrentColumn(_draggedCard);
                        _draggedCard.Status = targetStatus;
                        AddCardToTargetColumn(_draggedCard, targetStatus);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[KanbanWindow] Error moving card: {ex.Message}");
                }
            }
            
            _draggedCard = null;
            
            // Reset column opacity
            columnBorder.Opacity = 1.0;
        }
    }
    
    private void Column_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border && _draggedCard != null)
        {
            // Highlight the column when dragging over it
            border.Opacity = 0.8;
        }
    }
    
    private void Column_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Opacity = 1.0;
        }
    }
}

/// <summary>
/// View model for Kanban cards (Requirements 5.5)
/// </summary>
public class KanbanCardViewModel
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public KanbanStatus Status { get; set; }
}