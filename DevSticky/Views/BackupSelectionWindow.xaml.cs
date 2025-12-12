using System.Windows;
using System.Windows.Input;
using DevSticky.Interfaces;

namespace DevSticky.Views;

public partial class BackupSelectionWindow : Window
{
    public BackupInfo? SelectedBackup { get; private set; }

    public BackupSelectionWindow(IReadOnlyList<BackupInfo> backups)
    {
        InitializeComponent();
        
        // Convert to display model with size formatting
        var displayBackups = backups.Select(b => new BackupDisplayModel
        {
            FileName = b.FileName,
            FullPath = b.FullPath,
            CreatedAt = b.CreatedAt,
            SizeBytes = b.SizeBytes,
            NoteCount = b.NoteCount,
            SizeDisplay = FormatSize(b.SizeBytes)
        }).ToList();
        
        BackupList.ItemsSource = displayBackups;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

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

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is BackupDisplayModel selected)
        {
            SelectedBackup = new BackupInfo
            {
                FileName = selected.FileName,
                FullPath = selected.FullPath,
                CreatedAt = selected.CreatedAt,
                SizeBytes = selected.SizeBytes,
                NoteCount = selected.NoteCount
            };
            DialogResult = true;
            Close();
        }
    }

    private void BackupList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BtnRestore.IsEnabled = BackupList.SelectedItem != null;
    }

    private class BackupDisplayModel
    {
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public long SizeBytes { get; set; }
        public int NoteCount { get; set; }
        public string SizeDisplay { get; set; } = string.Empty;
    }
}
