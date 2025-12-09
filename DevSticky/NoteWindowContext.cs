using DevSticky.Interfaces;

namespace DevSticky;

/// <summary>
/// Dependency container for NoteWindow to avoid Service Locator pattern.
/// Provides all services needed by NoteWindow via constructor injection.
/// </summary>
public class NoteWindowContext
{
    public IThemeService ThemeService { get; }
    public IMonitorService MonitorService { get; }
    public ISnippetService SnippetService { get; }
    public IDebounceService DebounceService { get; }
    public IMarkdownService MarkdownService { get; }
    public ILinkService LinkService { get; }
    public INoteService NoteService { get; }

    public NoteWindowContext(
        IThemeService themeService,
        IMonitorService monitorService,
        ISnippetService snippetService,
        IDebounceService debounceService,
        IMarkdownService markdownService,
        ILinkService linkService,
        INoteService noteService)
    {
        ThemeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        MonitorService = monitorService ?? throw new ArgumentNullException(nameof(monitorService));
        SnippetService = snippetService ?? throw new ArgumentNullException(nameof(snippetService));
        DebounceService = debounceService ?? throw new ArgumentNullException(nameof(debounceService));
        MarkdownService = markdownService ?? throw new ArgumentNullException(nameof(markdownService));
        LinkService = linkService ?? throw new ArgumentNullException(nameof(linkService));
        NoteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
    }
}
