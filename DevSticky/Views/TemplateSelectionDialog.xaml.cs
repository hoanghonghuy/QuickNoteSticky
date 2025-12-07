using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;
using DevSticky.Models;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfBorder = System.Windows.Controls.Border;
using WpfGrid = System.Windows.Controls.Grid;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfRowDefinition = System.Windows.Controls.RowDefinition;
using WpfGridLength = System.Windows.GridLength;
using WpfFontWeights = System.Windows.FontWeights;
using WpfTextTrimming = System.Windows.TextTrimming;
using WpfTextWrapping = System.Windows.TextWrapping;
using WpfCornerRadius = System.Windows.CornerRadius;
using WpfThickness = System.Windows.Thickness;

namespace DevSticky.Views;

/// <summary>
/// Dialog for selecting a template when creating a new note.
/// Requirements: 6.1, 6.8
/// </summary>
public partial class TemplateSelectionDialog : Window
{
    private readonly ITemplateService _templateService;
    private IReadOnlyList<NoteTemplate> _allTemplates = Array.Empty<NoteTemplate>();
    private NoteTemplate? _selectedTemplate;
    private string? _selectedCategory;
    private string _searchQuery = string.Empty;

    /// <summary>
    /// The selected template, or null if user chose blank note or cancelled
    /// </summary>
    public NoteTemplate? SelectedTemplate => _selectedTemplate;

    /// <summary>
    /// True if user chose to create a blank note
    /// </summary>
    public bool CreateBlankNote { get; private set; }

    public TemplateSelectionDialog()
    {
        InitializeComponent();
        _templateService = App.GetService<ITemplateService>();
        Loaded += async (_, _) => await LoadTemplatesAsync();
    }

    private async Task LoadTemplatesAsync()
    {
        _allTemplates = await _templateService.GetAllTemplatesAsync();
        BuildCategoryFilters();
        RefreshTemplateList();
    }

    /// <summary>
    /// Build category filter buttons (Requirements 6.8)
    /// </summary>
    private void BuildCategoryFilters()
    {
        CategoryFilterPanel.Children.Clear();

        // Add "All" button
        var allBtn = CreateCategoryButton("All", null);
        CategoryFilterPanel.Children.Add(allBtn);

        // Add category buttons
        var categories = _allTemplates
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        foreach (var category in categories)
        {
            var btn = CreateCategoryButton(category, category);
            CategoryFilterPanel.Children.Add(btn);
        }

        // Select "All" by default
        UpdateCategoryButtonStyles();
    }

    private WpfButton CreateCategoryButton(string text, string? category)
    {
        var btn = new WpfButton
        {
            Content = text,
            Tag = category,
            Style = (Style)FindResource("CategoryFilterBtn")
        };
        btn.Click += CategoryFilter_Click;
        return btn;
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn)
        {
            _selectedCategory = btn.Tag as string;
            UpdateCategoryButtonStyles();
            RefreshTemplateList();
        }
    }

    private void UpdateCategoryButtonStyles()
    {
        foreach (var child in CategoryFilterPanel.Children)
        {
            if (child is WpfButton btn)
            {
                var isSelected = (btn.Tag as string) == _selectedCategory;
                btn.Background = isSelected
                    ? (WpfBrush)FindResource("BlueBrush")
                    : (WpfBrush)FindResource("Surface1Brush");
                btn.Foreground = isSelected
                    ? (WpfBrush)FindResource("BaseBrush")
                    : (WpfBrush)FindResource("SubtextBrush");
            }
        }
    }

    /// <summary>
    /// Handle search text changes (Requirements 6.8)
    /// </summary>
    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text.Trim().ToLowerInvariant();
        RefreshTemplateList();
    }

    /// <summary>
    /// Refresh the template list based on filters
    /// </summary>
    private void RefreshTemplateList()
    {
        var filtered = _allTemplates.AsEnumerable();

        // Apply category filter
        if (!string.IsNullOrEmpty(_selectedCategory))
        {
            filtered = filtered.Where(t => t.Category == _selectedCategory);
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            filtered = filtered.Where(t =>
                t.Name.ToLowerInvariant().Contains(_searchQuery) ||
                t.Description.ToLowerInvariant().Contains(_searchQuery) ||
                t.Category.ToLowerInvariant().Contains(_searchQuery));
        }

        BuildTemplateCards(filtered.ToList());
    }

    /// <summary>
    /// Build template cards for display (Requirements 6.1, 6.8)
    /// </summary>
    private void BuildTemplateCards(IReadOnlyList<NoteTemplate> templates)
    {
        TemplateList.Items.Clear();

        foreach (var template in templates)
        {
            var card = CreateTemplateCard(template);
            TemplateList.Items.Add(card);
        }
    }

    private WpfBorder CreateTemplateCard(NoteTemplate template)
    {
        var card = new WpfBorder
        {
            Width = 200,
            Height = 160,
            Style = (Style)FindResource("TemplateCard"),
            Tag = template
        };

        var grid = new WpfGrid();
        grid.RowDefinitions.Add(new WpfRowDefinition { Height = WpfGridLength.Auto });
        grid.RowDefinitions.Add(new WpfRowDefinition { Height = WpfGridLength.Auto });
        grid.RowDefinitions.Add(new WpfRowDefinition { Height = new WpfGridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new WpfRowDefinition { Height = WpfGridLength.Auto });

        // Icon and Name
        var headerPanel = new WpfStackPanel { Orientation = WpfOrientation.Horizontal };
        var icon = new WpfTextBlock
        {
            Text = template.IsBuiltIn ? "📋" : "📄",
            FontSize = 16,
            Margin = new WpfThickness(0, 0, 6, 0),
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        var nameText = new WpfTextBlock
        {
            Text = template.Name,
            FontSize = 14,
            FontWeight = WpfFontWeights.SemiBold,
            Foreground = (WpfBrush)FindResource("TextBrush"),
            TextTrimming = WpfTextTrimming.CharacterEllipsis,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        headerPanel.Children.Add(icon);
        headerPanel.Children.Add(nameText);
        WpfGrid.SetRow(headerPanel, 0);
        grid.Children.Add(headerPanel);

        // Category badge
        var categoryBadge = new WpfBorder
        {
            Background = (WpfBrush)FindResource("Surface1Brush"),
            CornerRadius = new WpfCornerRadius(3),
            Padding = new WpfThickness(6, 2, 6, 2),
            Margin = new WpfThickness(0, 6, 0, 0),
            HorizontalAlignment = WpfHorizontalAlignment.Left
        };
        var categoryText = new WpfTextBlock
        {
            Text = template.Category,
            FontSize = 10,
            Foreground = (WpfBrush)FindResource("SubtextBrush")
        };
        categoryBadge.Child = categoryText;
        WpfGrid.SetRow(categoryBadge, 1);
        grid.Children.Add(categoryBadge);

        // Description
        var descText = new WpfTextBlock
        {
            Text = template.Description,
            FontSize = 11,
            Foreground = (WpfBrush)FindResource("SubtextBrush"),
            TextWrapping = WpfTextWrapping.Wrap,
            TextTrimming = WpfTextTrimming.CharacterEllipsis,
            MaxHeight = 48,
            Margin = new WpfThickness(0, 8, 0, 0)
        };
        WpfGrid.SetRow(descText, 2);
        grid.Children.Add(descText);

        // Language badge
        var langBadge = new WpfBorder
        {
            Background = (WpfBrush)FindResource("BlueBrush"),
            CornerRadius = new WpfCornerRadius(3),
            Padding = new WpfThickness(6, 2, 6, 2),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Opacity = 0.8
        };
        var langText = new WpfTextBlock
        {
            Text = template.DefaultLanguage,
            FontSize = 10,
            Foreground = (WpfBrush)FindResource("BaseBrush")
        };
        langBadge.Child = langText;
        WpfGrid.SetRow(langBadge, 3);
        grid.Children.Add(langBadge);

        card.Child = grid;

        // Event handlers
        card.MouseLeftButtonUp += (_, _) => SelectTemplate(template, card);
        card.MouseEnter += (_, _) =>
        {
            if (card.Tag != _selectedTemplate)
                card.Background = (WpfBrush)FindResource("Surface1Brush");
        };
        card.MouseLeave += (_, _) =>
        {
            if (card.Tag != _selectedTemplate)
                card.Background = (WpfBrush)FindResource("Surface0Brush");
        };

        return card;
    }

    private void SelectTemplate(NoteTemplate template, WpfBorder card)
    {
        _selectedTemplate = template;

        // Update visual selection
        foreach (var item in TemplateList.Items)
        {
            if (item is WpfBorder b)
            {
                b.Background = (WpfBrush)FindResource("Surface0Brush");
                b.BorderBrush = null;
                b.BorderThickness = new WpfThickness(0);
            }
        }

        card.Background = (WpfBrush)FindResource("Surface1Brush");
        card.BorderBrush = (WpfBrush)FindResource("BlueBrush");
        card.BorderThickness = new WpfThickness(2);

        // Update footer
        SelectedTemplateText.Text = $"Selected: {template.Name}";
        BtnSelect.IsEnabled = true;
    }

    // Event Handlers
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Create a blank note without template (Requirements 6.1)
    /// </summary>
    private void BtnBlankNote_Click(object sender, RoutedEventArgs e)
    {
        CreateBlankNote = true;
        _selectedTemplate = null;
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Use the selected template (Requirements 6.2)
    /// </summary>
    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplate != null)
        {
            CreateBlankNote = false;
            DialogResult = true;
            Close();
        }
    }
}
