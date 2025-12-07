using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Views;

/// <summary>
/// Dialog for saving selected text as a code snippet.
/// Requirements: 3.1, 3.2
/// </summary>
public partial class SaveSnippetDialog : Window
{
    private readonly ISnippetService _snippetService;
    private readonly string _content;
    private IReadOnlyList<SnippetPlaceholder> _placeholders = Array.Empty<SnippetPlaceholder>();
    
    /// <summary>
    /// The created snippet, or null if cancelled
    /// </summary>
    public Snippet? CreatedSnippet { get; private set; }

    public SaveSnippetDialog(string content, string defaultLanguage = "PlainText")
    {
        InitializeComponent();
        _snippetService = App.GetService<ISnippetService>();
        _content = content;
        
        InitializeForm(defaultLanguage);
        DetectPlaceholders();
    }

    private async void InitializeForm(string defaultLanguage)
    {
        // Populate language combo
        LanguageCombo.ItemsSource = new[] 
        { 
            "PlainText", "CSharp", "Java", "JavaScript", "TypeScript", 
            "Json", "Xml", "Sql", "Python", "Bash" 
        };
        
        var langIndex = Array.IndexOf(
            new[] { "PlainText", "CSharp", "Java", "JavaScript", "TypeScript", "Json", "Xml", "Sql", "Python", "Bash" },
            defaultLanguage);
        LanguageCombo.SelectedIndex = langIndex >= 0 ? langIndex : 0;
        
        // Populate category combo with existing categories
        var snippets = await _snippetService.GetAllSnippetsAsync();
        var categories = snippets
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        
        if (!categories.Contains("General"))
            categories.Insert(0, "General");
        
        CategoryCombo.ItemsSource = categories;
        CategoryCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Detect placeholders in the content and show preview (Requirements 3.2)
    /// </summary>
    private void DetectPlaceholders()
    {
        _placeholders = _snippetService.ParsePlaceholders(_content);
        
        if (_placeholders.Count > 0)
        {
            PlaceholderPreview.Visibility = Visibility.Visible;
            PlaceholderList.ItemsSource = _placeholders;
        }
        else
        {
            PlaceholderPreview.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            CustomDialog.ShowWarning("Validation Error", "Please enter a name for the snippet.");
            NameBox.Focus();
            return;
        }
        
        // Parse tags
        var tags = TagsBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
        
        // Create snippet
        var snippet = new Snippet
        {
            Name = name,
            Description = DescriptionBox.Text.Trim(),
            Content = _content,
            Language = LanguageCombo.SelectedItem?.ToString() ?? "PlainText",
            Category = CategoryCombo.Text.Trim(),
            Tags = tags
        };
        
        try
        {
            CreatedSnippet = await _snippetService.CreateSnippetAsync(snippet);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            CustomDialog.ShowError("Save Failed", ex.Message);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
