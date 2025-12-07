using System.Windows.Input;
using DevSticky.Helpers;
using DevSticky.Interfaces;
using DevSticky.Models;

namespace DevSticky.ViewModels;

/// <summary>
/// ViewModel for a single note window
/// </summary>
public class NoteViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IFormatterService _formatterService;
    private readonly ISearchService _searchService;
    private readonly IDebounceService _debounceService;
    private readonly Action<NoteViewModel>? _onClose;
    private readonly Action? _onSave;

    private string _title = "Untitled Note";
    private string _content = string.Empty;
    private string _language = "PlainText";
    private bool _isPinned = true;
    private double _opacity = 0.9;
    private bool _isSearchVisible;
    private string _searchTerm = string.Empty;
    private int _currentMatchIndex;
    private IReadOnlyList<SearchMatch> _searchMatches = Array.Empty<SearchMatch>();
    private Guid? _groupId;
    private List<Guid> _tagIds = new();
    private string? _monitorDeviceId;

    public Guid Id { get; }
    public WindowRect WindowRect { get; set; }
    public DateTime CreatedDate { get; }
    public DateTime ModifiedDate { get; private set; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value?.Length > 50 ? value[..50] : value ?? "Untitled Note");
    }

    public Guid? GroupId
    {
        get => _groupId;
        set => SetProperty(ref _groupId, value);
    }

    public List<Guid> TagIds
    {
        get => _tagIds;
        set => SetProperty(ref _tagIds, value ?? new());
    }

    public string? MonitorDeviceId
    {
        get => _monitorDeviceId;
        set
        {
            if (SetProperty(ref _monitorDeviceId, value))
            {
                _debounceService.Debounce($"save_{Id}", () => Save(), 500);
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                _debounceService.Debounce($"save_{Id}", () => Save(), 500);
                UpdateSearchMatches();
            }
        }
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }


    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, OpacityHelper.Clamp(value));
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
                UpdateSearchMatches();
        }
    }

    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        set => SetProperty(ref _currentMatchIndex, value);
    }

    public IReadOnlyList<SearchMatch> SearchMatches
    {
        get => _searchMatches;
        private set => SetProperty(ref _searchMatches, value);
    }

    // Commands
    public ICommand CloseCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand FormatCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleSearchCommand { get; }
    public ICommand NextMatchCommand { get; }
    public ICommand PreviousMatchCommand { get; }
    public ICommand IncreaseOpacityCommand { get; }
    public ICommand DecreaseOpacityCommand { get; }
    public ICommand? NavigateToNoteCommand { get; set; }

    public NoteViewModel(
        Note note,
        INoteService noteService,
        IFormatterService formatterService,
        ISearchService searchService,
        IDebounceService debounceService,
        Action<NoteViewModel>? onClose = null,
        Action? onSave = null)
    {
        Id = note.Id;
        _title = note.Title;
        _content = note.Content;
        _language = note.Language;
        _isPinned = note.IsPinned;
        _opacity = note.Opacity;
        _groupId = note.GroupId;
        _tagIds = note.TagIds ?? new();
        _monitorDeviceId = note.MonitorDeviceId;
        WindowRect = note.WindowRect;
        CreatedDate = note.CreatedDate;
        ModifiedDate = note.ModifiedDate;

        _noteService = noteService;
        _formatterService = formatterService;
        _searchService = searchService;
        _debounceService = debounceService;
        _onClose = onClose;
        _onSave = onSave;

        CloseCommand = new RelayCommand(Close);
        TogglePinCommand = new RelayCommand(TogglePin);
        FormatCommand = new RelayCommand(Format);
        SaveCommand = new RelayCommand(Save);
        ToggleSearchCommand = new RelayCommand(ToggleSearch);
        NextMatchCommand = new RelayCommand(NextMatch);
        PreviousMatchCommand = new RelayCommand(PreviousMatch);
        IncreaseOpacityCommand = new RelayCommand(() => Opacity += 0.1);
        DecreaseOpacityCommand = new RelayCommand(() => Opacity -= 0.1);
    }

    private void Close() => _onClose?.Invoke(this);
    private void TogglePin() => IsPinned = !IsPinned;
    private void ToggleSearch() => IsSearchVisible = !IsSearchVisible;

    private void Format()
    {
        if (_formatterService.IsValidJson(Content))
            Content = _formatterService.FormatJson(Content);
        else if (_formatterService.IsValidXml(Content))
            Content = _formatterService.FormatXml(Content);
    }

    private void Save()
    {
        var note = ToNote();
        _noteService.UpdateNote(note);
        _onSave?.Invoke();
    }

    private void NextMatch()
    {
        if (SearchMatches.Count > 0)
            CurrentMatchIndex = _searchService.GetNextMatchIndex(CurrentMatchIndex, SearchMatches.Count);
    }

    private void PreviousMatch()
    {
        if (SearchMatches.Count > 0)
            CurrentMatchIndex = _searchService.GetPreviousMatchIndex(CurrentMatchIndex, SearchMatches.Count);
    }

    private void UpdateSearchMatches()
    {
        SearchMatches = _searchService.FindMatches(Content, SearchTerm);
        CurrentMatchIndex = 0;
    }

    public Note ToNote() => new()
    {
        Id = Id,
        Title = Title,
        Content = Content,
        Language = Language,
        IsPinned = IsPinned,
        Opacity = Opacity,
        GroupId = GroupId,
        TagIds = TagIds,
        MonitorDeviceId = MonitorDeviceId,
        WindowRect = WindowRect,
        CreatedDate = CreatedDate,
        ModifiedDate = ModifiedDate
    };
}
