using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RestaurantManager.Views;

public partial class ReportFilterWindow : Window
{
    public enum ReportMode { ByItem, ByCashier }

    public ReportMode Mode { get; set; }
    public DateTime FilterFrom { get; private set; }
    public DateTime FilterTo { get; private set; }
    public string? SelectedKey { get; private set; } // null = all
    public string? SelectedLabel { get; private set; }
    public bool Confirmed { get; private set; }

    private string _activeFilter = "today";
    private List<(string Key, string Label)> _allItems = new();

    public ReportFilterWindow(ReportMode mode, List<(string Key, string Label)> items)
    {
        InitializeComponent();
        Mode = mode;
        _allItems = items;

        if (mode == ReportMode.ByItem)
        {
            HeaderTitle.Text = "📦 Report by Item";
            HeaderSub.Text = "Select an item and date range to print";
            SelectionLabel.Text = "📦 SELECT ITEM";
            SelectionHint.Text = "Choose a specific item or 'All Items' for a full report";
        }
        else
        {
            HeaderTitle.Text = "👤 Report by Cashier";
            HeaderSub.Text = "Select a cashier and date range to print";
            SelectionLabel.Text = "👤 SELECT CASHIER";
            SelectionHint.Text = "Choose a specific cashier or 'All Cashiers' for a full report";
        }

        // Default to today
        FilterFrom = DateTime.Now.Date;
        FilterTo = DateTime.Now.Date;
        DateFromPicker.SelectedDate = FilterFrom;
        DateToPicker.SelectedDate = FilterTo;
        HighlightDateBtn("today");
        UpdatePeriodLabel();
        PopulateList("");
    }

    private void PopulateList(string search)
    {
        SelectionList.Items.Clear();

        var allLabel = Mode == ReportMode.ByItem ? "📋 All Items" : "📋 All Cashiers";
        var allItem = new ListBoxItem { Content = allLabel, Tag = "__all__", FontWeight = FontWeights.SemiBold };
        SelectionList.Items.Add(allItem);

        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allItems
            : _allItems.Where(i => i.Label.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var (key, label) in filtered)
        {
            SelectionList.Items.Add(new ListBoxItem { Content = label, Tag = key });
        }

        if (SelectionList.Items.Count > 0)
            SelectionList.SelectedIndex = 0;
    }

    // ─── DATE FILTER ───

    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string filter) return;
        _activeFilter = filter;

        var today = DateTime.Now.Date;
        switch (filter)
        {
            case "yesterday":
                FilterFrom = today.AddDays(-1);
                FilterTo = today.AddDays(-1);
                break;
            case "today":
                FilterFrom = today;
                FilterTo = today;
                break;
            case "week":
                FilterFrom = today.AddDays(-(int)today.DayOfWeek);
                FilterTo = today;
                break;
            case "month":
                FilterFrom = new DateTime(today.Year, today.Month, 1);
                FilterTo = today;
                break;
            case "custom":
                break;
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

    // ─── SEARCH & SELECTION ───

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        PopulateList(SearchBox.Text.Trim());
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    {
        PrintBtn.IsEnabled = SelectionList.SelectedItem != null;
    }

    // ─── ACTIONS ───

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (SelectionList.SelectedItem is ListBoxItem item)
        {
            var key = item.Tag?.ToString();
            if (key == "__all__")
            {
                SelectedKey = null;
                SelectedLabel = "All";
            }
            else
            {
                SelectedKey = key;
                SelectedLabel = item.Content?.ToString() ?? "";
            }
        }

        Confirmed = true;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
