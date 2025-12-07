using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DevSticky.Services;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace DevSticky.Views;

public enum DialogType
{
    Info,
    Success,
    Warning,
    Error,
    Question
}

public enum DialogButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel
}

public partial class CustomDialog : Window
{
    public bool? Result { get; private set; }
    
    private CustomDialog()
    {
        InitializeComponent();
    }

    private void SetupDialog(string title, string message, DialogType type, DialogButtons buttons)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        
        // Set icon and color based on type
        var (icon, color) = type switch
        {
            DialogType.Info => ("ℹ️", "#89B4FA"),
            DialogType.Success => ("✅", "#A6E3A1"),
            DialogType.Warning => ("⚠️", "#F9E2AF"),
            DialogType.Error => ("❌", "#F38BA8"),
            DialogType.Question => ("❓", "#89B4FA"),
            _ => ("ℹ️", "#89B4FA")
        };
        
        IconText.Text = icon;
        
        // Setup buttons
        ButtonPanel.Children.Clear();
        
        switch (buttons)
        {
            case DialogButtons.OK:
                AddButton(L.Get("OK"), true, true);
                break;
                
            case DialogButtons.OKCancel:
                AddButton(L.Get("Cancel"), false, false);
                AddButton(L.Get("OK"), true, true);
                break;
                
            case DialogButtons.YesNo:
                AddButton(L.Get("No"), false, false);
                AddButton(L.Get("Yes"), true, true, type == DialogType.Warning || type == DialogType.Error);
                break;
                
            case DialogButtons.YesNoCancel:
                AddButton(L.Get("Cancel"), null, false);
                AddButton(L.Get("No"), false, false);
                AddButton(L.Get("Yes"), true, true, type == DialogType.Warning);
                break;
        }
    }

    private void AddButton(string text, bool? result, bool isPrimary, bool isDanger = false)
    {
        var btn = new Button
        {
            Content = text,
            Margin = new Thickness(8, 0, 0, 0)
        };
        
        if (isDanger)
            btn.Style = (Style)FindResource("DangerBtn");
        else if (isPrimary)
            btn.Style = (Style)FindResource("PrimaryBtn");
        else
            btn.Style = (Style)FindResource("DialogBtn");
        
        btn.Click += (_, _) =>
        {
            Result = result;
            DialogResult = result ?? false;
            Close();
        };
        
        ButtonPanel.Children.Add(btn);
    }

    // Static factory methods
    public static void ShowInfo(string title, string message, Window? owner = null)
    {
        Show(title, message, DialogType.Info, DialogButtons.OK, owner);
    }

    public static void ShowSuccess(string title, string message, Window? owner = null)
    {
        Show(title, message, DialogType.Success, DialogButtons.OK, owner);
    }

    public static void ShowWarning(string title, string message, Window? owner = null)
    {
        Show(title, message, DialogType.Warning, DialogButtons.OK, owner);
    }

    public static void ShowError(string title, string message, Window? owner = null)
    {
        Show(title, message, DialogType.Error, DialogButtons.OK, owner);
    }

    public static bool Confirm(string title, string message, Window? owner = null)
    {
        return Show(title, message, DialogType.Question, DialogButtons.YesNo, owner) == true;
    }

    public static bool ConfirmWarning(string title, string message, Window? owner = null)
    {
        return Show(title, message, DialogType.Warning, DialogButtons.YesNo, owner) == true;
    }

    public static bool? Show(string title, string message, DialogType type, DialogButtons buttons, Window? owner = null)
    {
        var dialog = new CustomDialog();
        dialog.SetupDialog(title, message, type, buttons);
        
        if (owner != null)
            dialog.Owner = owner;
        else if (Application.Current.MainWindow?.IsVisible == true)
            dialog.Owner = Application.Current.MainWindow;
        
        dialog.ShowDialog();
        return dialog.Result;
    }
}
