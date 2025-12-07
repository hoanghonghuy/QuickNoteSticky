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
    public ICommand SettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public TrayViewModel(
        Action onNewNote,
        Action onShowAll,
        Action onHideAll,
        Action onSettings,
        Action onExit)
    {
        NewNoteCommand = new RelayCommand(onNewNote);
        ShowAllCommand = new RelayCommand(onShowAll);
        HideAllCommand = new RelayCommand(onHideAll);
        SettingsCommand = new RelayCommand(onSettings);
        ExitCommand = new RelayCommand(onExit);
    }
}
