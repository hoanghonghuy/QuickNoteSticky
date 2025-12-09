using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using DevSticky.Resources;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;

namespace DevSticky.Views;

/// <summary>
/// Snippet browser window with tree view organized by category,
/// search functionality, and snippet preview panel.
/// Requirements: 3.3, 3.9, 3.10
/// </summary>
public partial class SnippetBrowserWindow : Window
{
    private readonly ISnippetService _snippetService;
    private readonly IThemeService? _themeService;
    private IReadOnlyList<Snippet> _allSnippets = Array.Empty<Snippet>();
    private Snippet? _selectedSnippet;
    
    /// <summary>
    /// Event raised when user wants to insert a snippet
    /// </summary>
    public event EventHandler<Snippet>? SnippetInsertRequested;

    public SnippetBrowserWindow()
    {
        InitializeComponent();
        _snippetService = App.GetService<ISnippetService>();
        
        try
        {
            _themeService = App.GetService<IThemeService>();
            _themeService.ThemeChanged += OnThemeChanged;
        }
        catch { /* Service not available during design time */ }
        
        Loaded += async (_, _) => await LoadSnippetsAsync();
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        Dispatcher.Invoke(() => UpdatePreviewHighlighting());
    }

    private async Task LoadSnippetsAsync()
    {
        _allSnippets = await _snippetService.GetAllSnippetsAsync();
        BuildCategoryTree(_allSnippets);
    }

    /// <summary>
    /// Build the category tree view from snippets (Requirements 3.9)
    /// </summary>
    private void BuildCategoryTree(IReadOnlyList<Snippet> snippets)
    {
        CategoryTree.Items.Clear();
        
        // Group snippets by category
        var categories = snippets
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var category in categories)
        {
            var categoryItem = new TreeViewItem
            {
                Header = $"📁 {category.Key} ({category.Count()})",
                IsExpanded = true,
                Tag = category.Key
            };

            foreach (var snippet in category.OrderBy(s => s.Name))
            {
                var snippetItem = new TreeViewItem
                {
                    Header = $"📄 {snippet.Name}",
                    Tag = snippet
                };
                categoryItem.Items.Add(snippetItem);
            }

            CategoryTree.Items.Add(categoryItem);
        }
    }

    /// <summary>
    /// Handle search text changes for real-time filtering (Requirements 3.10)
    /// </summary>
    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var query = SearchBox.Text.Trim();
            
            if (string.IsNullOrEmpty(query))
            {
                BuildCategoryTree(_allSnippets);
            }
            else
            {
                var filtered = await _snippetService.SearchSnippetsAsync(query);
                BuildCategoryTree(filtered);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle tree view selection changes
    /// </summary>
    private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is Snippet snippet)
        {
            SelectSnippet(snippet);
        }
        else
        {
            ClearSelection();
        }
    }

    /// <summary>
    /// Display selected snippet in preview panel
    /// </summary>
    private void SelectSnippet(Snippet snippet)
    {
        _selectedSnippet = snippet;
        
        SnippetNameText.Text = snippet.Name;
        SnippetDescText.Text = snippet.Description;
        SnippetLangText.Text = snippet.Language;
        TagsPanel.ItemsSource = snippet.Tags;
        
        PreviewEditor.Text = snippet.Content;
        UpdatePreviewHighlighting();
        
        // Show placeholders if any
        if (snippet.Placeholders.Count > 0)
        {
            PlaceholdersPanel.Visibility = Visibility.Visible;
            PlaceholdersList.ItemsSource = snippet.Placeholders;
        }
        else
        {
            PlaceholdersPanel.Visibility = Visibility.Collapsed;
        }
        
        BtnInsert.IsEnabled = true;
        BtnDelete.IsEnabled = true;
    }

    private void ClearSelection()
    {
        _selectedSnippet = null;
        SnippetNameText.Text = "Select a snippet";
        SnippetDescText.Text = "";
        SnippetLangText.Text = "";
        TagsPanel.ItemsSource = null;
        PreviewEditor.Text = "";
        PlaceholdersPanel.Visibility = Visibility.Collapsed;
        BtnInsert.IsEnabled = false;
        BtnDelete.IsEnabled = false;
    }

    private void UpdatePreviewHighlighting()
    {
        if (_selectedSnippet == null) return;
        
        var highlighting = _selectedSnippet.Language switch
        {
            "CSharp" => "C#",
            "JavaScript" or "TypeScript" => "JavaScript",
            "Json" => "Json",
            "Xml" => "XML",
            "Sql" => "TSQL",
            "Python" => "Python",
            "Java" => "Java",
            "Bash" => "Boo",
            _ => null
        };
        
        if (highlighting != null)
        {
            try
            {
                var definition = HighlightingManager.Instance.GetDefinition(highlighting);
                if (definition != null)
                {
                    var isDarkTheme = _themeService?.CurrentTheme == Theme.Dark;
                    if (isDarkTheme != false)
                    {
                        VSCodeDarkTheme.ApplyTheme(definition);
                    }
                    else
                    {
                        VSCodeLightTheme.ApplyTheme(definition);
                    }
                    PreviewEditor.SyntaxHighlighting = definition;
                }
            }
            catch { PreviewEditor.SyntaxHighlighting = null; }
        }
        else
        {
            PreviewEditor.SyntaxHighlighting = null;
        }
    }

    // Event Handlers
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Insert selected snippet (Requirements 3.4)
    /// </summary>
    private void BtnInsert_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSnippet != null)
        {
            SnippetInsertRequested?.Invoke(this, _selectedSnippet);
            Close();
        }
    }

    /// <summary>
    /// Delete selected snippet
    /// </summary>
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSnippet == null) return;
        
        try
        {
            if (CustomDialog.Confirm("Delete Snippet", 
                $"Are you sure you want to delete '{_selectedSnippet.Name}'?"))
            {
                await _snippetService.DeleteSnippetAsync(_selectedSnippet.Id);
                await LoadSnippetsAsync();
                ClearSelection();
            }
        }
        catch (Exception ex)
        {
            CustomDialog.ShowError("Delete Failed", ex.Message);
        }
    }

    /// <summary>
    /// Export all snippets to JSON (Requirements 3.7)
    /// </summary>
    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = "snippets-export.json"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _snippetService.ExportSnippetsAsync(dialog.FileName);
                CustomDialog.ShowSuccess("Export Complete", 
                    $"Snippets exported to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError("Export Failed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Import snippets from JSON (Requirements 3.8)
    /// </summary>
    private async void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json"
        };
        
        if (dialog.ShowDialog() == true)
        {
            // Show conflict resolution dialog
            var resolution = ShowConflictResolutionDialog();
            if (resolution == null) return;
            
            try
            {
                await _snippetService.ImportSnippetsAsync(dialog.FileName, resolution.Value);
                await LoadSnippetsAsync();
                CustomDialog.ShowSuccess("Import Complete", "Snippets imported successfully.");
            }
            catch (Exception ex)
            {
                CustomDialog.ShowError("Import Failed", ex.Message);
            }
        }
    }

    /// <summary>
    /// Show dialog for conflict resolution strategy
    /// </summary>
    private ConflictResolution? ShowConflictResolutionDialog()
    {
        var result = CustomDialog.Show(
            "Import Conflict Resolution",
            "How should conflicts be handled when importing snippets with the same ID?",
            DialogType.Question,
            DialogButtons.YesNoCancel);
        
        return result switch
        {
            true => ConflictResolution.Replace,  // Yes = Replace
            false => ConflictResolution.KeepBoth, // No = Keep Both
            _ => null // Cancel
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
        base.OnClosed(e);
    }
}
