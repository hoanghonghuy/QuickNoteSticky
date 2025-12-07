using System.Diagnostics;
using System.Windows;
using DevSticky.Interfaces;
using DevSticky.Models;
using Microsoft.Web.WebView2.Core;

namespace DevSticky.Views;

/// <summary>
/// UserControl for rendering markdown content as HTML using WebView2
/// Requirements: 4.2, 4.4
/// </summary>
public partial class MarkdownPreviewControl : System.Windows.Controls.UserControl
{
    private IMarkdownService? _markdownService;
    private IThemeService? _themeService;
    private INoteService? _noteService;
    private bool _isInitialized;
    private string _pendingHtml = string.Empty;

    /// <summary>
    /// Event raised when an external link is clicked
    /// </summary>
    public event EventHandler<string>? ExternalLinkClicked;

    /// <summary>
    /// Event raised when an internal note link is clicked
    /// </summary>
    public event EventHandler<Guid>? NoteLinkClicked;

    public MarkdownPreviewControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _markdownService = App.GetService<IMarkdownService>();
            _themeService = App.GetService<IThemeService>();
            _noteService = App.GetService<INoteService>();

            if (_themeService != null)
            {
                _themeService.ThemeChanged += OnThemeChanged;
            }

            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize MarkdownPreviewControl: {ex.Message}");
            LoadingText.Text = "Preview unavailable";
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            // Initialize WebView2 with default environment
            await WebView.EnsureCoreWebView2Async();

            // Configure WebView2 settings
            WebView.CoreWebView2.Settings.IsScriptEnabled = true;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            // Handle navigation to intercept link clicks
            WebView.CoreWebView2.NavigationStarting += OnNavigationStarting;

            _isInitialized = true;
            LoadingText.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;

            // Render any pending content
            if (!string.IsNullOrEmpty(_pendingHtml))
            {
                WebView.NavigateToString(_pendingHtml);
                _pendingHtml = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
            LoadingText.Text = "WebView2 not available";
        }
    }

    /// <summary>
    /// Update the preview with new markdown content
    /// </summary>
    /// <param name="markdown">The markdown content to render</param>
    public void UpdateContent(string markdown)
    {
        if (_markdownService == null)
            return;

        var options = new MarkdownOptions
        {
            EnableSyntaxHighlighting = true,
            EnableTables = true,
            EnableTaskLists = true,
            CurrentTheme = _themeService?.CurrentTheme ?? Theme.Dark
        };

        var html = _markdownService.RenderToHtml(markdown, options);

        if (_isInitialized)
        {
            WebView.NavigateToString(html);
        }
        else
        {
            // Store for later when WebView2 is ready
            _pendingHtml = html;
        }
    }

    /// <summary>
    /// Handle navigation events to intercept link clicks
    /// Requirements: 4.6, 4.7
    /// </summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Allow initial content load and about:blank
        if (e.Uri.StartsWith("data:") || e.Uri == "about:blank")
            return;

        // Cancel navigation - we'll handle it ourselves
        e.Cancel = true;

        // Check if it's an internal note link
        if (TryParseNoteLink(e.Uri, out var noteId))
        {
            NoteLinkClicked?.Invoke(this, noteId);
        }
        else
        {
            // External link - open in default browser
            ExternalLinkClicked?.Invoke(this, e.Uri);
            OpenExternalLink(e.Uri);
        }
    }

    /// <summary>
    /// Try to parse a note link from a URI
    /// Note links are in format: devsticky://note/{guid}
    /// </summary>
    private static bool TryParseNoteLink(string uri, out Guid noteId)
    {
        noteId = Guid.Empty;

        if (uri.StartsWith("devsticky://note/", StringComparison.OrdinalIgnoreCase))
        {
            var guidPart = uri.Substring("devsticky://note/".Length);
            return Guid.TryParse(guidPart, out noteId);
        }

        // Also check for [[note-id]] format that might be in href
        if (uri.Contains("[[") && uri.Contains("]]"))
        {
            var start = uri.IndexOf("[[") + 2;
            var end = uri.IndexOf("]]");
            if (end > start)
            {
                var guidPart = uri.Substring(start, end - start);
                // Handle [[guid|text]] format
                var pipeIndex = guidPart.IndexOf('|');
                if (pipeIndex > 0)
                    guidPart = guidPart.Substring(0, pipeIndex);
                return Guid.TryParse(guidPart, out noteId);
            }
        }

        return false;
    }

    /// <summary>
    /// Open an external link in the default browser
    /// </summary>
    private static void OpenExternalLink(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open external link: {ex.Message}");
        }
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        // Re-render content with new theme if we have content
        // The parent control should call UpdateContent again
    }

    /// <summary>
    /// Get the current HTML content (for export)
    /// </summary>
    public string GetCurrentHtml()
    {
        return _pendingHtml;
    }
}
