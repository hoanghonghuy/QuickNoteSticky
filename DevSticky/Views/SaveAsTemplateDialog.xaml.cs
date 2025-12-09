using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.Views;

/// <summary>
/// Dialog for saving a note as a template.
/// Requirements: 6.7
/// </summary>
public partial class SaveAsTemplateDialog : Window
{
    private readonly ITemplateService _templateService;
    private readonly Note _sourceNote;
    private IReadOnlyList<TemplatePlaceholder> _placeholders = Array.Empty<TemplatePlaceholder>();

    /// <summary>
    /// The created template, or null if cancelled
    /// </summary>
    public NoteTemplate? CreatedTemplate { get; private set; }

    public SaveAsTemplateDialog(Note sourceNote)
    {
        InitializeComponent();
        _templateService = App.GetService<ITemplateService>();
        _sourceNote = sourceNote;

        InitializeForm();
        DetectPlaceholders();
    }

    private async void InitializeForm()
    {
        try
        {
            // Pre-fill name from note title
            NameBox.Text = _sourceNote.Title;

            // Populate language combo
            LanguageCombo.ItemsSource = new[]
            {
                "PlainText", "Markdown", "CSharp", "Java", "JavaScript", "TypeScript",
                "Json", "Xml", "Sql", "Python", "Bash"
            };

            var langIndex = Array.IndexOf(
                new[] { "PlainText", "Markdown", "CSharp", "Java", "JavaScript", "TypeScript", "Json", "Xml", "Sql", "Python", "Bash" },
                _sourceNote.Language);
            LanguageCombo.SelectedIndex = langIndex >= 0 ? langIndex : 0;

            // Populate category combo with existing categories
            var templates = await _templateService.GetAllTemplatesAsync();
            var categories = templates
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            if (!categories.Contains("General"))
                categories.Insert(0, "General");
            if (!categories.Contains("Personal"))
                categories.Add("Personal");
            if (!categories.Contains("Development"))
                categories.Add("Development");
            if (!categories.Contains("Meeting"))
                categories.Add("Meeting");

            CategoryCombo.ItemsSource = categories.Distinct().OrderBy(c => c).ToList();
            CategoryCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize form: {ex.Message}");
            // Set defaults on failure
            LanguageCombo.ItemsSource = new[] { "PlainText" };
            LanguageCombo.SelectedIndex = 0;
            CategoryCombo.ItemsSource = new[] { "General", "Personal", "Development", "Meeting" };
            CategoryCombo.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Detect placeholders in the note content (Requirements 6.7)
    /// </summary>
    private void DetectPlaceholders()
    {
        _placeholders = _templateService.ParsePlaceholders(_sourceNote.Content);

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
            CustomDialog.ShowWarning("Validation Error", "Please enter a name for the template.");
            NameBox.Focus();
            return;
        }

        try
        {
            // Create template from note
            CreatedTemplate = await _templateService.CreateTemplateFromNoteAsync(
                _sourceNote,
                name,
                DescriptionBox.Text.Trim(),
                CategoryCombo.Text.Trim());

            // Update default language if changed
            if (CreatedTemplate != null)
            {
                CreatedTemplate.DefaultLanguage = LanguageCombo.SelectedItem?.ToString() ?? "PlainText";
                await _templateService.UpdateTemplateAsync(CreatedTemplate);
            }

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
