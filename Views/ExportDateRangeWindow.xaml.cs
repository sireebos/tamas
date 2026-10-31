using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantManager.Views;

public partial class ExportDateRangeWindow : Window
{
    public DateTime FilterFrom { get; private set; }
    public DateTime FilterTo { get; private set; }
    public bool Confirmed { get; private set; }

    private string _activeFilter = "today";

    public ExportDateRangeWindow(string exportType = "CSV")
    {
        InitializeComponent();
        FilterFrom = DateTime.Now.Date;
        FilterTo = DateTime.Now.Date;
        DateFromPicker.SelectedDate = FilterFrom;
        DateToPicker.SelectedDate = FilterTo;
        HighlightDateBtn("today");
        UpdatePeriodLabel();

        if (exportType == "PDF")
        {
            HeaderTitle.Text = "📄 Export PDF Report";
            ExportIcon.Text = "📄";
            ExportLabel.Text = "Export PDF";
        }
        else
        {
            HeaderTitle.Text = "📥 Export CSV Report";
            ExportIcon.Text = "📥";
            ExportLabel.Text = "Export CSV";
        }
    }

    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string filter) return;
        _activeFilter = filter;
        var today = DateTime.Now.Date;
        switch (filter)
        {
            case "yesterday": FilterFrom = today.AddDays(-1); FilterTo = today.AddDays(-1); break;
            case "today": FilterFrom = today; FilterTo = today; break;
            case "week": FilterFrom = today.AddDays(-(int)today.DayOfWeek); FilterTo = today; break;
            case "month": FilterFrom = new DateTime(today.Year, today.Month, 1); FilterTo = today; break;
            case "custom": break;
        }
        CustomDatePanel.Visibility = filter == "custom" ? Visibility.Visible : Visibility.Collapsed;
        if (filter != "custom")
        {
            DateFromPicker.SelectedDate = FilterFrom;
            DateToPicker.SelectedDate = FilterTo;
        }
        HighlightDateBtn(filter);
        UpdatePeriodLabel();
    }

    private void CustomDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DateFromPicker.SelectedDate.HasValue) FilterFrom = DateFromPicker.SelectedDate.Value;
        if (DateToPicker.SelectedDate.HasValue) FilterTo = DateToPicker.SelectedDate.Value;
        _activeFilter = "custom";
        HighlightDateBtn("custom");
        UpdatePeriodLabel();
    }

    private void HighlightDateBtn(string active)
    {
        var activeBg = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var activeFg = Brushes.White;
        var normalBg = Brushes.White;
        var normalFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));

        foreach (var btn in new[] { BtnYesterday, BtnToday, BtnWeek, BtnMonth, BtnCustom })
        {
            bool isActive = btn.Tag?.ToString() == active;
            btn.Background = isActive ? activeBg : normalBg;
            btn.Foreground = isActive ? activeFg : normalFg;
        }
    }

    private void UpdatePeriodLabel()
    {
        PeriodLabel.Text = _activeFilter switch
        {
            "yesterday" => $"Yesterday — {FilterFrom:MMM dd, yyyy}",
            "today" => $"Today — {FilterFrom:MMM dd, yyyy}",
            "week" => $"This Week — {FilterFrom:MMM dd} to {FilterTo:MMM dd, yyyy}",
            "month" => $"This Month — {FilterFrom:MMMM yyyy}",
            _ => $"{FilterFrom:MMM dd, yyyy} — {FilterTo:MMM dd, yyyy}"
        };
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
