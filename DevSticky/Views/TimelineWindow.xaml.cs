using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DevSticky.Interfaces;
using DevSticky.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DevSticky.Views;

/// <summary>
/// Timeline window for viewing notes chronologically
/// </summary>
public partial class TimelineWindow : Window
{
    private readonly ITimelineService? _timelineService;
    private readonly INoteService? _noteService;
    private readonly IWindowService? _windowService;
    
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private DateTime _fromDisplayMonth = DateTime.Today;
    private DateTime _toDisplayMonth = DateTime.Today;
    
    public ObservableCollection<TimelineItemViewModel> TimelineItems { get; } = new();
    
    public TimelineWindow()
    {
        InitializeComponent();
        
        try
        {
            _timelineService = App.ServiceProvider.GetRequiredService<ITimelineService>();
            _noteService = App.ServiceProvider.GetRequiredService<INoteService>();
            _windowService = App.ServiceProvider.GetRequiredService<IWindowService>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TimelineWindow] Failed to resolve services: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to initialize Timeline: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }
        
        TimelineContent.ItemsSource = TimelineItems;
        
        Loaded += TimelineWindow_Loaded;
    }
    
    private async void TimelineWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTimelineDataAsync();
    }
    
    /// <summary>
    /// Build custom calendar grid for a panel
    /// </summary>
    private void BuildCalendarGrid(StackPanel panel, DateTime displayMonth, bool isFromCalendar)
    {
        panel.Children.Clear();
        
        var darkBg = (SolidColorBrush)FindResource("Surface0Brush");
        var darkText = (SolidColorBrush)FindResource("TextBrush");
        var subText = (SolidColorBrush)FindResource("SubtextBrush");
        var blueBrush = (SolidColorBrush)FindResource("BlueBrush");
        var surface2 = (SolidColorBrush)FindResource("Surface2Brush");
        var baseBrush = (SolidColorBrush)FindResource("BaseBrush");
        
        // Header with month/year and navigation
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var prevBtn = new System.Windows.Controls.Button { 
            Content = "◀"
        };
        prevBtn.Style = (Style)FindResource("NavBtn");
        prevBtn.Click += (s, e) => {
            if (isFromCalendar) { _fromDisplayMonth = _fromDisplayMonth.AddMonths(-1); BuildCalendarGrid(panel, _fromDisplayMonth, true); }
            else { _toDisplayMonth = _toDisplayMonth.AddMonths(-1); BuildCalendarGrid(panel, _toDisplayMonth, false); }
        };
        Grid.SetColumn(prevBtn, 0);
        header.Children.Add(prevBtn);
        
        var monthLabel = new TextBlock { 
            Text = displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            Foreground = darkText, FontWeight = FontWeights.SemiBold, FontSize = 14,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        Grid.SetColumn(monthLabel, 1);
        header.Children.Add(monthLabel);
        
        var nextBtn = new System.Windows.Controls.Button { 
            Content = "▶"
        };
        nextBtn.Style = (Style)FindResource("NavBtn");
        nextBtn.Click += (s, e) => {
            if (isFromCalendar) { _fromDisplayMonth = _fromDisplayMonth.AddMonths(1); BuildCalendarGrid(panel, _fromDisplayMonth, true); }
            else { _toDisplayMonth = _toDisplayMonth.AddMonths(1); BuildCalendarGrid(panel, _toDisplayMonth, false); }
        };
        Grid.SetColumn(nextBtn, 2);
        header.Children.Add(nextBtn);
        
        panel.Children.Add(header);
        
        // Day names header
        var dayNames = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        string[] days = { "T2", "T3", "T4", "T5", "T6", "T7", "CN" };
        for (int i = 0; i < 7; i++)
        {
            dayNames.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var dayLabel = new TextBlock { Text = days[i], Foreground = subText, FontSize = 11, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
            Grid.SetColumn(dayLabel, i);
            dayNames.Children.Add(dayLabel);
        }
        panel.Children.Add(dayNames);
        
        // Calendar grid
        var calGrid = new Grid();
        for (int i = 0; i < 7; i++) calGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 6; i++) calGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        var firstDay = new DateTime(displayMonth.Year, displayMonth.Month, 1);
        int startDayOfWeek = ((int)firstDay.DayOfWeek + 6) % 7; // Monday = 0
        int daysInMonth = DateTime.DaysInMonth(displayMonth.Year, displayMonth.Month);
        
        int row = 0, col = startDayOfWeek;
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(displayMonth.Year, displayMonth.Month, day);
            var btn = new System.Windows.Controls.Button { Content = day.ToString(), Width = 32, Height = 32, Tag = date };
            
            // Apply DayBtn style from resources - ensure it overrides any default styles
            btn.Style = (Style)FindResource("DayBtn");
            btn.OverridesDefaultStyle = true;
            
            // Today highlight - add border
            if (date == DateTime.Today)
            {
                btn.BorderThickness = new Thickness(1);
                btn.BorderBrush = blueBrush;
            }
            
            // Selected highlight
            var selectedDate = isFromCalendar ? _fromDate : _toDate;
            if (selectedDate.HasValue && date == selectedDate.Value.Date)
            {
                btn.Background = blueBrush;
                btn.Foreground = baseBrush;
            }
            
            btn.Click += async (s, e) => {
                var clickedDate = (DateTime)((System.Windows.Controls.Button)s).Tag;
                if (isFromCalendar) {
                    _fromDate = clickedDate;
                    FromDateText.Text = clickedDate.ToString("dd/MM/yyyy");
                    FromDatePopup.IsOpen = false;
                    // Rebuild calendar to update selected state
                    BuildCalendarGrid(FromCalendarPanel, _fromDisplayMonth, true);
                } else {
                    _toDate = clickedDate;
                    ToDateText.Text = clickedDate.ToString("dd/MM/yyyy");
                    ToDatePopup.IsOpen = false;
                    // Rebuild calendar to update selected state
                    BuildCalendarGrid(ToCalendarPanel, _toDisplayMonth, false);
                }
                if (IsLoaded) await LoadTimelineDataAsync();
            };
            
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            calGrid.Children.Add(btn);
            
            col++;
            if (col > 6) { col = 0; row++; }
        }
        panel.Children.Add(calGrid);
    }

    
    /// <summary>
    /// Load timeline data and populate the view
    /// </summary>
    private async Task LoadTimelineDataAsync()
    {
        if (_timelineService == null)
        {
            System.Diagnostics.Debug.WriteLine("[TimelineWindow] TimelineService not available");
            return;
        }
        
        try
        {
            var timelineItems = await _timelineService.GetTimelineItemsAsync(_fromDate, _toDate);
            if (timelineItems == null)
            {
                System.Diagnostics.Debug.WriteLine("[TimelineWindow] No timeline items returned");
                return;
            }
            var groupedItems = _timelineService.GroupByDate(timelineItems);
            
            TimelineItems.Clear();
            
            foreach (var dateGroup in groupedItems.OrderByDescending(g => g.Key))
            {
                bool isFirstItemOfDay = true;
                
                foreach (var item in dateGroup.Value)
                {
                    var viewModel = new TimelineItemViewModel
                    {
                        NoteId = item.NoteId,
                        Title = item.Title,
                        ContentPreview = item.ContentPreview,
                        CreatedDate = item.CreatedDate,
                        ModifiedDate = item.ModifiedDate,
                        Tags = item.Tags.ToList(),
                        ShowDateHeader = isFirstItemOfDay,
                        DateHeader = dateGroup.Key.ToString("dddd, MMMM dd, yyyy", CultureInfo.CurrentCulture),
                        CreatedTime = item.CreatedDate.ToString("HH:mm", CultureInfo.CurrentCulture),
                        HasContent = !string.IsNullOrEmpty(item.ContentPreview),
                        HasTags = item.Tags.Any(),
                        ShowModifiedInfo = item.ModifiedDate.Date != item.CreatedDate.Date,
                        ModifiedInfo = item.ModifiedDate.Date != item.CreatedDate.Date 
                            ? $"Modified: {item.ModifiedDate:MMM dd, yyyy HH:mm}"
                            : string.Empty
                    };
                    
                    TimelineItems.Add(viewModel);
                    isFirstItemOfDay = false;
                }
            }
            
            UpdateItemCount();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading timeline: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void UpdateItemCount()
    {
        ItemCountText.Text = $"{TimelineItems.Count} {L.Get("Items")}";
    }
    
    private void TimelineItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_noteService == null || _windowService == null) return;
        
        if (sender is Border border && border.Tag is Guid noteId)
        {
            var note = _noteService.GetNoteById(noteId);
            if (note != null)
            {
                _windowService.ShowNote(note);
            }
        }
    }
    
    private void FromDateButton_Click(object sender, RoutedEventArgs e)
    {
        BuildCalendarGrid(FromCalendarPanel, _fromDisplayMonth, true);
        FromDatePopup.IsOpen = true;
    }
    
    private void ToDateButton_Click(object sender, RoutedEventArgs e)
    {
        BuildCalendarGrid(ToCalendarPanel, _toDisplayMonth, false);
        ToDatePopup.IsOpen = true;
    }
    
    private async void BtnClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _fromDate = null;
        _toDate = null;
        FromDateText.Text = "Select date";
        ToDateText.Text = "Select date";
        await LoadTimelineDataAsync();
    }
    
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTimelineDataAsync();
    }
    
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

/// <summary>
/// ViewModel for timeline items with display properties
/// </summary>
public class TimelineItemViewModel
{
    public Guid NoteId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentPreview { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool ShowDateHeader { get; set; }
    public string DateHeader { get; set; } = string.Empty;
    public string CreatedTime { get; set; } = string.Empty;
    public bool HasContent { get; set; }
    public bool HasTags { get; set; }
    public bool ShowModifiedInfo { get; set; }
    public string ModifiedInfo { get; set; } = string.Empty;
}
