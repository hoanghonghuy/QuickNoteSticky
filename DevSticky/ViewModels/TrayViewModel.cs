using System.Windows.Input;

namespace DevSticky.ViewModels;

/// <summary>
/// ViewModel for system tray icon and menu
/// </summary>
public class TrayViewModel : ViewModelBase
{
    public ICommand NewNoteCommand { get; }
    public ICommand ShowAllCommand { get; }
    public ICommand HideAllCommand { get; }
    public ICommand ToggleVisibilityCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }

    private bool _notesVisible = true;

    public TrayViewModel(
        Action onNewNote,
        Action onShowAll,
        Action onHideAll,
        Action onSettings,
        Action onExit)
    {
        NewNoteCommand = new RelayCommand(onNewNote);
        ShowAllCommand = new RelayCommand(() =>
        {
            onShowAll();
            _notesVisible = true;
        });
        HideAllCommand = new RelayCommand(() =>
        {
            onHideAll();
            _notesVisible = false;
        });
        ToggleVisibilityCommand = new RelayCommand(() =>
        {
            if (_notesVisible)
            {
                onHideAll();
                _notesVisible = false;
            }
            else
            {
                onShowAll();
                _notesVisible = true;
            }
        });
        SettingsCommand = new RelayCommand(onSettings);
        ExitCommand = new RelayCommand(onExit);
    }
}
