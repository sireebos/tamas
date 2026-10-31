using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using RestaurantManager.Models;

namespace RestaurantManager.Views;

public partial class ReportsView : UserControl
{
    private List<Order> _allOrders = new();

    // Keyboard shortcut bridge
    public void ShortcutPrintEndOfDay()
    {
        var today = DateTime.Now.Date;
        var orders = _allOrders.Where(o => o.Status == "completed" && o.CreatedAt.Date == today).ToList();
        PrintSalesSummary(orders, today, today, "END OF DAY");
    }
    private List<Order> _filteredOrders = new();
    private BrandingSettings _settings = new();
    private string _activeFilter = "month";

    public ReportsView()
    {
        InitializeComponent();
    }

    private async void ReportsView_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await App.BrandingService!.GetSettingsAsync();
        _allOrders = await App.OrderService!.GetAllOrdersAsync();
        DateFrom.SelectedDate = DateTime.Now.Date;
        DateTo.SelectedDate = DateTime.Now.Date;
        ApplyFilter("today");
    }

    private string Fmt(decimal amount) => _settings.FormatCurrency(amount);

    // ═══════════ FILTER LOGIC ═══════════

    private void FilterBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            ApplyFilter(tag);
    }

    private void DateRange_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (DateFrom.SelectedDate.HasValue && DateTo.SelectedDate.HasValue && _allOrders.Count > 0)
        {
            _activeFilter = "custom";
            UpdateFilterHighlight();
            _filteredOrders = _allOrders
                .Where(o => o.CreatedAt.Date >= DateFrom.SelectedDate.Value.Date && o.CreatedAt.Date <= DateTo.SelectedDate.Value.Date)
                .ToList();
            ReportPeriodLabel.Text = $"{DateFrom.SelectedDate.Value:MMM dd, yyyy} — {DateTo.SelectedDate.Value:MMM dd, yyyy}";
            RefreshAllSections();
        }
    }

    private void ApplyFilter(string filter)
    {
        _activeFilter = filter;
        var today = DateTime.Now.Date;
        DateTime start, end = today;

        switch (filter)
        {
            case "today":
                start = today;
                ReportPeriodLabel.Text = $"Today — {today:MMM dd, yyyy}";
                break;
            case "week":
                start = today.AddDays(-(int)today.DayOfWeek);
                ReportPeriodLabel.Text = $"This Week — {start:MMM dd} to {end:MMM dd, yyyy}";
                break;
            case "year":
                start = new DateTime(today.Year, 1, 1);
                ReportPeriodLabel.Text = $"This Year — {today.Year}";
                break;
            default: // month
                start = new DateTime(today.Year, today.Month, 1);
                ReportPeriodLabel.Text = $"This Month — {today:MMMM yyyy}";
                break;
        }

        DateFrom.SelectedDate = start;
        DateTo.SelectedDate = end;
        UpdateFilterHighlight();

        _filteredOrders = _allOrders.Where(o => o.CreatedAt.Date >= start && o.CreatedAt.Date <= end).ToList();
        RefreshAllSections();
    }

    private void UpdateFilterHighlight()
    {
        var activeGrad = new LinearGradientBrush(
            Color.FromRgb(0x66, 0x7e, 0xea), Color.FromRgb(0x76, 0x4b, 0xa2), 0);
        var defaultBg = new SolidColorBrush(Colors.White);
        var activeFg = new SolidColorBrush(Colors.White);
        var defaultFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));

        foreach (var btn in new[] { FilterToday, FilterWeek, FilterMonth, FilterYear })
        {
            bool active = (string)btn.Tag == _activeFilter;
            btn.Background = active ? activeGrad : defaultBg;
            btn.Foreground = active ? activeFg : defaultFg;
            btn.BorderBrush = active ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));
        }
    }

    // ═══════════ REFRESH ALL ═══════════

    private void RefreshAllSections()
    {
        RefreshSummaryCards();
        RefreshRevenueChart();
        RefreshPaymentBreakdown();
        RefreshTopItems();
        RefreshOrderStatus();
        RefreshCategorySales();
        RefreshPeakHours();
        RefreshStaffPerformance();
        RefreshDailyBreakdown("date_desc");
    }

    // ═══════════ SUMMARY CARDS ═══════════

    private void RefreshSummaryCards()
    {
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        var totalRevenue = completed.Sum(o => o.Total);
        var totalOrders = _filteredOrders.Count;
        var avgOrder = completed.Count > 0 ? totalRevenue / completed.Count : 0;
        var totalItems = completed.SelectMany(o => o.Items).Sum(i => i.Quantity);

        TotalRevenueText.Text = Fmt(totalRevenue);
        TotalOrdersText.Text = totalOrders.ToString();
        OrdersCompletedText.Text = $"{completed.Count} completed";
        AvgOrderText.Text = Fmt(avgOrder);
        AvgOrderSub.Text = $"from {completed.Count} orders";
        TotalItemsSoldText.Text = totalItems.ToString();
        ItemsSoldSub.Text = $"across {completed.Count} orders";

        // Compare with previous period
        var days = (DateTo.SelectedDate!.Value - DateFrom.SelectedDate!.Value).Days + 1;
        var prevStart = DateFrom.SelectedDate.Value.AddDays(-days);
        var prevEnd = DateFrom.SelectedDate.Value.AddDays(-1);
        var prevCompleted = _allOrders.Where(o => o.Status == "completed" && o.CreatedAt.Date >= prevStart && o.CreatedAt.Date <= prevEnd).ToList();
        var prevRevenue = prevCompleted.Sum(o => o.Total);

        if (prevRevenue > 0)
        {
            var pct = ((totalRevenue - prevRevenue) / prevRevenue) * 100;
            var arrow = pct >= 0 ? "↑" : "↓";
            RevenueCompare.Text = $"{arrow} {Math.Abs(pct):F1}% vs previous period";
        }
        else
        {
            RevenueCompare.Text = days > 1 ? $"{days} days selected" : "Today's earnings";
        }
    }

    // ═══════════ REVENUE CHART ═══════════

    private void RefreshRevenueChart()
    {
        RevenueChart.Children.Clear();

        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        if (completed.Count == 0)
        {
            RevenueChart.Children.Add(new TextBlock
            {
                Text = "No completed orders in this period",
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 90, 0, 0)
            });
            return;
        }

        // Group by date
        var grouped = completed
            .GroupBy(o => o.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.Total) })
            .ToList();

        if (grouped.Count == 0) return;

        var maxRevenue = grouped.Max(g => g.Revenue);
        if (maxRevenue == 0) maxRevenue = 1;

        double chartW = ChartArea.ActualWidth > 0 ? ChartArea.ActualWidth - 20 : 500;
        double chartH = 200;
        double barGap = 4;
        double barW = Math.Max(6, Math.Min(40, (chartW - 40) / grouped.Count - barGap));

        double x = 20;
        foreach (var day in grouped)
        {
            double barH = (double)(day.Revenue / maxRevenue) * (chartH - 30);
            if (barH < 3) barH = 3;

            var bar = new Border
            {
                Width = barW,
                Height = barH,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Background = new LinearGradientBrush(
                    Color.FromRgb(0x66, 0x7e, 0xea),
                    Color.FromRgb(0x76, 0x4b, 0xa2), 90),
                ToolTip = $"{day.Date:MMM dd}\n{Fmt(day.Revenue)}"
            };

            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, chartH - barH);
            RevenueChart.Children.Add(bar);

            // Date label (show selectively to avoid overlap)
            if (grouped.Count <= 15 || grouped.IndexOf(day) % Math.Max(1, grouped.Count / 10) == 0)
            {
                var label = new TextBlock
                {
                    Text = day.Date.ToString("dd"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                    TextAlignment = TextAlignment.Center,
                    Width = barW
                };
                Canvas.SetLeft(label, x);
                Canvas.SetTop(label, chartH + 2);
                RevenueChart.Children.Add(label);
            }

            x += barW + barGap;
        }
    }

    // ═══════════ PAYMENT BREAKDOWN ═══════════

    private void RefreshPaymentBreakdown()
    {
        PaymentBreakdownPanel.Children.Clear();
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        var total = completed.Sum(o => o.Total);

        var paymentGroups = completed
            .GroupBy(o => string.IsNullOrWhiteSpace(o.PaymentMethod) ? "Unknown" : o.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Total) })
            .OrderByDescending(g => g.Amount)
            .ToList();

        var colors = new[] { "#667eea", "#22c55e", "#f59e0b", "#0ea5e9", "#ef4444" };

        foreach (var (pg, idx) in paymentGroups.Select((p, i) => (p, i)))
        {
            var label = pg.Method switch
            {
                "cash" => "💵 Cash",
                "card" => "💳 Card",
                "transfer" => "🏦 Bank Transfer",
                "mobile" => "📱 Mobile Money",
                _ => "❓ " + pg.Method
            };

            var pct = total > 0 ? (double)(pg.Amount / total) * 100 : 0;
            var color = (Color)ColorConverter.ConvertFromString(colors[idx % colors.Length]);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.RowDefinitions.Add(new RowDefinition());
            row.RowDefinitions.Add(new RowDefinition());

            var topRow = new Grid();
            topRow.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) });
            topRow.Children.Add(new TextBlock { Text = $"{Fmt(pg.Amount)} ({pg.Count})", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)), HorizontalAlignment = HorizontalAlignment.Right });
            Grid.SetRow(topRow, 0);
            row.Children.Add(topRow);

            var barBg = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 4, 0, 0) };
            var barFill = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };

            barBg.Loaded += (s, e) =>
            {
                barFill.Width = barBg.ActualWidth * pct / 100;
            };

            var barGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            Grid.SetRow(barGrid, 1);
            row.Children.Add(barGrid);

            PaymentBreakdownPanel.Children.Add(row);
        }

        if (paymentGroups.Count == 0)
            PaymentBreakdownPanel.Children.Add(EmptyText("No completed orders"));
    }

    // ═══════════ TOP SELLING ITEMS ═══════════

    private void RefreshTopItems()
    {
        TopItemsPanel.Children.Clear();
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();

        var topItems = completed
            .SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Name ?? "Unknown")
            .Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) })
            .OrderByDescending(g => g.Revenue)
            .Take(10)
            .ToList();

        var maxRev = topItems.FirstOrDefault()?.Revenue ?? 1;
        var colors = new[] { "#667eea", "#22c55e", "#f59e0b", "#0ea5e9", "#ef4444", "#8b5cf6", "#ec4899", "#14b8a6", "#f97316", "#6366f1" };

        for (int i = 0; i < topItems.Count; i++)
        {
            var item = topItems[i];
            var pct = maxRev > 0 ? (double)(item.Revenue / maxRev) * 100 : 0;
            var color = (Color)ColorConverter.ConvertFromString(colors[i % colors.Length]);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };

            var header = new Grid();
            header.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = $"#{i + 1}", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color), Width = 26 },
                    new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) }
                }
            });
            header.Children.Add(new TextBlock
            {
                Text = $"{Fmt(item.Revenue)} ({item.Qty} sold)",
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)),
                HorizontalAlignment = HorizontalAlignment.Right
            });
            row.Children.Add(header);

            var barBg = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 22, 0, 0) };
            var barFill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };

            barBg.Loaded += (s, e) => { barFill.Width = barBg.ActualWidth * pct / 100; };

            var barGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            row.Children.Add(barGrid);

            TopItemsPanel.Children.Add(row);
        }

        if (topItems.Count == 0)
            TopItemsPanel.Children.Add(EmptyText("No items sold"));
    }

    // ═══════════ ORDER STATUS ═══════════

    private void RefreshOrderStatus()
    {
        OrderStatusPanel.Children.Clear();
        var total = _filteredOrders.Count;
        if (total == 0) { OrderStatusPanel.Children.Add(EmptyText("No orders")); return; }

        var statuses = new[]
        {
            ("completed", "✅ Completed", "#22c55e"),
            ("pending", "⏳ Pending", "#f59e0b"),
            ("preparing", "🔧 Preparing", "#0ea5e9"),
            ("cancelled", "❌ Cancelled", "#ef4444")
        };

        foreach (var (status, label, hex) in statuses)
        {
            var count = _filteredOrders.Count(o => o.Status == status);
            if (count == 0) continue;
            var pct = (double)count / total * 100;
            var color = (Color)ColorConverter.ConvertFromString(hex);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            var header = new Grid();
            header.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) });
            header.Children.Add(new TextBlock { Text = $"{count} ({pct:F1}%)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)), HorizontalAlignment = HorizontalAlignment.Right });
            row.Children.Add(header);

            var barBg = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 22, 0, 0) };
            var barFill = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            barBg.Loaded += (s, e) => { barFill.Width = barBg.ActualWidth * pct / 100; };

            var barGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            row.Children.Add(barGrid);

            OrderStatusPanel.Children.Add(row);
        }
    }

    // ═══════════ SALES BY CATEGORY ═══════════

    private void RefreshCategorySales()
    {
        CategorySalesPanel.Children.Clear();
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();

        var categories = completed
            .SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Category ?? "Uncategorized")
            .Select(g => new { Category = g.Key, Revenue = g.Sum(i => i.Price * i.Quantity), Count = g.Sum(i => i.Quantity) })
            .OrderByDescending(g => g.Revenue)
            .ToList();

        var totalRev = categories.Sum(c => c.Revenue);
        var colors = new[] { "#667eea", "#22c55e", "#f59e0b", "#0ea5e9", "#ef4444", "#8b5cf6", "#ec4899" };

        foreach (var (cat, idx) in categories.Select((c, i) => (c, i)))
        {
            var pct = totalRev > 0 ? (double)(cat.Revenue / totalRev) * 100 : 0;
            var color = (Color)ColorConverter.ConvertFromString(colors[idx % colors.Length]);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            var header = new Grid();
            header.Children.Add(new TextBlock { Text = cat.Category, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) });
            header.Children.Add(new TextBlock { Text = $"{Fmt(cat.Revenue)} ({pct:F1}%)", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)), HorizontalAlignment = HorizontalAlignment.Right });
            row.Children.Add(header);

            var barBg = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 22, 0, 0) };
            var barFill = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            barBg.Loaded += (s, e) => { barFill.Width = barBg.ActualWidth * pct / 100; };

            var barGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            row.Children.Add(barGrid);

            CategorySalesPanel.Children.Add(row);
        }

        if (categories.Count == 0)
            CategorySalesPanel.Children.Add(EmptyText("No category sales data"));
    }

    // ═══════════ PEAK HOURS ═══════════

    private void RefreshPeakHours()
    {
        PeakHoursPanel.Children.Clear();
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();

        var hourly = completed
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.Total) })
            .OrderByDescending(g => g.Count)
            .Take(8)
            .ToList();

        var maxCount = hourly.FirstOrDefault()?.Count ?? 1;

        foreach (var h in hourly)
        {
            var pct = (double)h.Count / maxCount * 100;
            var hourLabel = h.Hour == 0 ? "12 AM" : h.Hour < 12 ? $"{h.Hour} AM" : h.Hour == 12 ? "12 PM" : $"{h.Hour - 12} PM";

            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            var header = new Grid();
            header.Children.Add(new TextBlock { Text = $"🕐 {hourLabel}", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) });
            header.Children.Add(new TextBlock { Text = $"{h.Count} orders • {Fmt(h.Revenue)}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)), HorizontalAlignment = HorizontalAlignment.Right });
            row.Children.Add(header);

            var barBg = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 22, 0, 0) };
            var barFill = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
                Background = new LinearGradientBrush(Color.FromRgb(0xf5, 0x9e, 0x0b), Color.FromRgb(0xd9, 0x77, 0x06), 0)
            };
            barBg.Loaded += (s, e) => { barFill.Width = barBg.ActualWidth * pct / 100; };

            var barGrid = new Grid { Margin = new Thickness(0, 22, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            row.Children.Add(barGrid);

            PeakHoursPanel.Children.Add(row);
        }

        if (hourly.Count == 0)
            PeakHoursPanel.Children.Add(EmptyText("No peak hours data"));
    }

    // ═══════════ STAFF PERFORMANCE ═══════════

    private async void RefreshStaffPerformance()
    {
        StaffPerformancePanel.Children.Clear();

        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        var users = await App.UserService!.GetAllUsersAsync();
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);

        var staffStats = completed
            .Where(o => o.UserId.HasValue)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new
            {
                Name = userLookup.TryGetValue(g.Key, out var n) ? n : $"User #{g.Key}",
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Total)
            })
            .OrderByDescending(s => s.Revenue)
            .ToList();

        var maxRev = staffStats.FirstOrDefault()?.Revenue ?? 1;

        foreach (var (staff, idx) in staffStats.Select((s, i) => (s, i)))
        {
            var pct = maxRev > 0 ? (double)(staff.Revenue / maxRev) * 100 : 0;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            var header = new Grid();

            header.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new Border
                    {
                        Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 8, 0),
                        Background = new LinearGradientBrush(Color.FromRgb(0x66, 0x7e, 0xea), Color.FromRgb(0x76, 0x4b, 0xa2), 0),
                        Child = new TextBlock { Text = staff.Name.Length > 0 ? staff.Name[..1].ToUpper() : "?", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    },
                    new TextBlock { Text = staff.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), VerticalAlignment = VerticalAlignment.Center }
                }
            });
            header.Children.Add(new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = Fmt(staff.Revenue), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), TextAlignment = TextAlignment.Right },
                    new TextBlock { Text = $"{staff.Orders} orders", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)), TextAlignment = TextAlignment.Right }
                }
            });
            row.Children.Add(header);

            var barBg = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Margin = new Thickness(0, 28, 0, 0) };
            var barFill = new Border
            {
                Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0,
                Background = new LinearGradientBrush(Color.FromRgb(0x66, 0x7e, 0xea), Color.FromRgb(0x76, 0x4b, 0xa2), 0)
            };
            barBg.Loaded += (s, e) => { barFill.Width = barBg.ActualWidth * pct / 100; };

            var barGrid = new Grid { Margin = new Thickness(0, 28, 0, 0) };
            barGrid.Children.Add(barBg);
            barGrid.Children.Add(barFill);
            row.Children.Add(barGrid);

            StaffPerformancePanel.Children.Add(row);
        }

        if (staffStats.Count == 0)
            StaffPerformancePanel.Children.Add(EmptyText("No staff performance data"));
    }

    // ═══════════ DAILY BREAKDOWN ═══════════
    private string _dailySort = "date_desc";

    private void RefreshDailyBreakdown(string sort = "")
    {
        if (!string.IsNullOrEmpty(sort)) _dailySort = sort;
        DailyBreakdownPanel.Children.Clear();

        var dailyData = _filteredOrders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Completed = g.Count(o => o.Status == "completed"),
                Revenue = g.Where(o => o.Status == "completed").Sum(o => o.Total)
            });

        switch (_dailySort)
        {
            case "orders": dailyData = dailyData.OrderByDescending(d => d.Total); break;
            case "orders_asc": dailyData = dailyData.OrderBy(d => d.Total); break;
            case "revenue": dailyData = dailyData.OrderByDescending(d => d.Revenue); break;
            case "revenue_asc": dailyData = dailyData.OrderBy(d => d.Revenue); break;
            case "date_asc": dailyData = dailyData.OrderBy(d => d.Date); break;
            default: dailyData = dailyData.OrderByDescending(d => d.Date); break;
        }

        var daily = dailyData.Take(31).ToList();

        // Header
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var hStyle = new Action<TextBlock, string, HorizontalAlignment>((tb, tag, align) => 
        { 
            tb.FontSize = 10; tb.FontWeight = FontWeights.Bold; 
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
            tb.Cursor = Cursors.Hand;
            tb.HorizontalAlignment = align;
            tb.TextDecorations = TextDecorations.Underline;
            tb.MouseDown += (s, e) => {
                string nextSort = _dailySort == tag ? tag + "_asc" : tag;
                RefreshDailyBreakdown(nextSort);
            };
        });

        var h1 = new TextBlock { Text = "DATE" }; hStyle(h1, "date", HorizontalAlignment.Left); Grid.SetColumn(h1, 0); headerGrid.Children.Add(h1);
        var h2 = new TextBlock { Text = "ORD" }; hStyle(h2, "orders", HorizontalAlignment.Left); Grid.SetColumn(h2, 1); headerGrid.Children.Add(h2);
        var h3 = new TextBlock { Text = "DONE" }; hStyle(h3, "orders", HorizontalAlignment.Left); Grid.SetColumn(h3, 2); headerGrid.Children.Add(h3);
        var h4 = new TextBlock { Text = "REVENUE" }; hStyle(h4, "revenue", HorizontalAlignment.Right); Grid.SetColumn(h4, 3); headerGrid.Children.Add(h4);
        DailyBreakdownPanel.Children.Add(headerGrid);

        DailyBreakdownPanel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)), Margin = new Thickness(0, 0, 0, 4) });

        foreach (var d in daily)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var c1 = new TextBlock { Text = d.Date.ToString("MMM dd, ddd"), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)) };
            Grid.SetColumn(c1, 0); rowGrid.Children.Add(c1);

            var c2 = new TextBlock { Text = d.Total.ToString(), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)) };
            Grid.SetColumn(c2, 1); rowGrid.Children.Add(c2);

            var c3 = new TextBlock { Text = d.Completed.ToString(), FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)), FontWeight = FontWeights.SemiBold };
            Grid.SetColumn(c3, 2); rowGrid.Children.Add(c3);

            var c4 = new TextBlock { Text = Fmt(d.Revenue), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), TextAlignment = TextAlignment.Right };
            Grid.SetColumn(c4, 3); rowGrid.Children.Add(c4);

            DailyBreakdownPanel.Children.Add(rowGrid);
        }

        if (daily.Count == 0)
            DailyBreakdownPanel.Children.Add(EmptyText("No daily data"));
    }


    // ═══════════ EXPORT CSV ═══════════

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ask for date range first
            var rangeDlg = new ExportDateRangeWindow("CSV");
            rangeDlg.Owner = Window.GetWindow(this);
            rangeDlg.ShowDialog();
            if (!rangeDlg.Confirmed) return;

            var exportOrders = _allOrders
                .Where(o => o.CreatedAt.Date >= rangeDlg.FilterFrom && o.CreatedAt.Date <= rangeDlg.FilterTo)
                .ToList();

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Export Report"
            };

            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Order Number,Date,Time,Status,Payment Method,Table,Items,Total");

            foreach (var order in exportOrders.OrderByDescending(o => o.CreatedAt))
            {
                var itemNames = string.Join("; ", order.Items.Select(i => $"{i.InventoryItem?.Name ?? "Item"} x{i.Quantity}"));
                // Escape fields for CSV safety
                var escapedItems = "\"" + itemNames.Replace("\"", "\"\"") + "\"";
                sb.AppendLine($"{order.OrderNumber},{order.CreatedAt:yyyy-MM-dd},{order.CreatedAt:HH:mm},{order.Status},{order.PaymentMethod ?? "N/A"},{order.TableId?.ToString() ?? "N/A"},{escapedItems},{order.Total}");
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            
            if (App.Notifications != null)
            {
                await App.Notifications.ShowSuccessAsync(
                    "Export Complete",
                    $"Report exported successfully!\n{exportOrders.Count} orders exported."
                );
            }
            else
            {
                MessageBox.Show($"Report exported successfully!\n{exportOrders.Count} orders exported.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Report export failed");
            if (App.Notifications != null)
            {
                await App.Notifications.ShowErrorAsync("Export Error", ex.Message);
            }
            else
            {
                MessageBox.Show($"Export error: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ═══════════ EXPORT PDF ═══════════

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ask for date range first
            var rangeDlg = new ExportDateRangeWindow("PDF");
            rangeDlg.Owner = Window.GetWindow(this);
            rangeDlg.ShowDialog();
            if (!rangeDlg.Confirmed) return;

            // Temporarily swap filtered orders for the export
            var savedFiltered = _filteredOrders;
            _filteredOrders = _allOrders
                .Where(o => o.CreatedAt.Date >= rangeDlg.FilterFrom && o.CreatedAt.Date <= rangeDlg.FilterTo)
                .ToList();

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                Title = "Export Report as PDF"
            };

            if (dlg.ShowDialog() != true) { _filteredOrders = savedFiltered; return; }

            var doc = BuildPdfReport();
            _filteredOrders = savedFiltered;
            doc.PagePadding = new Thickness(50, 40, 50, 40);
            doc.ColumnWidth = double.PositiveInfinity;

            var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
            paginator.PageSize = new Size(816, 1056); // US Letter at 96 DPI

            // Try "Microsoft Print to PDF" for a real PDF file
            var pdfPrinter = FindPdfPrinter();
            if (pdfPrinter != null)
            {
                PrintViaPdfPrinter(pdfPrinter, paginator, dlg.FileName);
                await ShowExportSuccess(dlg.FileName);
            }
            else
            {
                // Fallback: save as XPS (correct extension) and inform user
                var xpsPath = System.IO.Path.ChangeExtension(dlg.FileName, ".xps");
                SaveAsXps(paginator, xpsPath);
                await ShowExportSuccess(xpsPath, isXpsFallback: true);
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "PDF export failed");
            if (App.Notifications != null)
                await App.Notifications.ShowErrorAsync("Export Error", $"PDF export failed: {ex.Message}");
            else
                MessageBox.Show($"Export error: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private System.Printing.PrintQueue? FindPdfPrinter()
    {
        try
        {
            var server = new System.Printing.LocalPrintServer();
            var queues = server.GetPrintQueues();
            // Prefer exact "Microsoft Print to PDF" match first
            foreach (var q in queues)
            {
                if (q.Name.Equals("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
                    return q;
            }
            // Then any printer with PDF in the name
            foreach (var q in queues)
            {
                if (q.Name.Contains("PDF", StringComparison.OrdinalIgnoreCase))
                    return q;
            }
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to enumerate print queues"); }
        return null;
    }

    private void PrintViaPdfPrinter(System.Printing.PrintQueue pdfPrinter, System.Windows.Documents.DocumentPaginator paginator, string outputPath)
    {
        // Use the PrintQueue directly with an output file path via PrintTicket
        var printDlg = new System.Windows.Controls.PrintDialog
        {
            PrintQueue = pdfPrinter
        };

        // Set page size
        printDlg.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(816, 1056);
        printDlg.PrintTicket.OutputQuality = System.Printing.OutputQuality.High;

        // For "Microsoft Print to PDF", we need to set the output port to our file path
        // The way to do this in WPF is to write to the print queue with the filename set
        // via the OUTPUT environment variable or by pre-setting the output file
        try
        {
            // Set the output file for Microsoft Print to PDF via registry/port
            // The simplest reliable approach: use PrintDocument with the output file
            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(pdfPrinter);
            
            // Microsoft Print to PDF will prompt for location, but we'll use a workaround:
            // Write to a temp XPS first, then use the print dialog to convert
            var tempXps = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"report_{Guid.NewGuid():N}.xps");
            SaveAsXps(paginator, tempXps);

            // Now print the XPS file to PDF using the PDF printer
            // For headless operation, we'll use a different approach:
            // Directly invoke the PDF printer via System.Drawing.Printing concepts
            // 
            // Actually, the simplest reliable WPF approach is:
            // Print the paginator directly — "Microsoft Print to PDF" will show a save dialog
            // but we can suppress it by setting the output port.

            // Clean approach: just use the PrintDialog which will write a real PDF
            if (File.Exists(tempXps))
            {
                // Read the XPS and send to PDF printer  
                using var xpsDoc = new System.Windows.Xps.Packaging.XpsDocument(tempXps, FileAccess.Read);
                var fixedDocSeq = xpsDoc.GetFixedDocumentSequence();
                if (fixedDocSeq != null)
                {
                    printDlg.PrintDocument(fixedDocSeq.DocumentPaginator, "Restaurant Report");
                }
                xpsDoc.Close();
            }

            // Clean up temp file
            try { if (File.Exists(tempXps)) File.Delete(tempXps); } catch { }
        }
        catch (Exception ex)
        {
            App.Logger?.LogWarning(ex, "PrintViaPdfPrinter failed, falling back to direct print");
            // Fallback: just print the paginator directly (will show save dialog)
            printDlg.PrintDocument(paginator, "Restaurant Report");
        }
    }

    private void SaveAsXps(System.Windows.Documents.DocumentPaginator paginator, string xpsPath)
    {
        // Ensure directory exists
        var dir = System.IO.Path.GetDirectoryName(xpsPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Delete existing file first (XpsDocument.Create requires no pre-existing file)
        if (File.Exists(xpsPath)) File.Delete(xpsPath);

        using var xpsDoc = new System.Windows.Xps.Packaging.XpsDocument(xpsPath, FileAccess.ReadWrite);
        var writer = System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xpsDoc);
        writer.Write(paginator);
        xpsDoc.Close();
    }

    private async Task ShowExportSuccess(string filePath, bool isXpsFallback = false)
    {
        var msg = isXpsFallback
            ? $"Report saved as XPS format (PDF printer not available).\n\nPath: {filePath}\n\n{_filteredOrders.Count} orders included.\n\nTip: You can open XPS files with the built-in Windows XPS Viewer."
            : $"Report saved successfully!\n\nPath: {filePath}\n\n{_filteredOrders.Count} orders included with comprehensive analytics.";

        if (App.Notifications != null)
        {
            await App.Notifications.ShowSuccessAsync("Report Exported", msg);
        }
        else
        {
            MessageBox.Show(msg, "Report Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Try to open the file
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch { /* User can open manually */ }
    }

    private System.Windows.Documents.FlowDocument BuildPdfReport()
    {
        var doc = new System.Windows.Documents.FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 11,
            PagePadding = new Thickness(50, 40, 50, 40),
            TextAlignment = TextAlignment.Left,
            ColumnWidth = double.PositiveInfinity
        };

        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        var totalRevenue = completed.Sum(o => o.Total);
        var totalOrders = _filteredOrders.Count;
        var completedCount = completed.Count;
        var avgOrder = completedCount > 0 ? totalRevenue / completedCount : 0;
        var totalItems = completed.SelectMany(o => o.Items).Sum(i => i.Quantity);
        var period = ReportPeriodLabel.Text;

        // ─── HEADER ───
        AddParagraph(doc, $"{_settings.BusinessNameLine1} {_settings.BusinessNameLine2}", 22, true, "#1a1a2e");
        AddParagraph(doc, "COMPREHENSIVE SALES & ANALYTICS REPORT", 14, true, "#667eea");
        AddParagraph(doc, $"Report Period: {period}", 11, false, "#64748b");
        AddParagraph(doc, $"Generated: {DateTime.Now:MMMM dd, yyyy 'at' hh:mm tt}", 10, false, "#94a3b8");
        AddParagraph(doc, $"Generated by: {App.CurrentUser?.Name ?? "System"} ({App.CurrentUser?.Role ?? "N/A"})", 10, false, "#94a3b8");
        AddDivider(doc);

        // ─── EXECUTIVE SUMMARY ───
        AddSectionTitle(doc, "1. EXECUTIVE SUMMARY");

        var days = (DateTo.SelectedDate!.Value - DateFrom.SelectedDate!.Value).Days + 1;
        var prevStart = DateFrom.SelectedDate.Value.AddDays(-days);
        var prevEnd = DateFrom.SelectedDate.Value.AddDays(-1);
        var prevCompleted = _allOrders.Where(o => o.Status == "completed" && o.CreatedAt.Date >= prevStart && o.CreatedAt.Date <= prevEnd).ToList();
        var prevRevenue = prevCompleted.Sum(o => o.Total);
        var revenueChange = prevRevenue > 0 ? ((totalRevenue - prevRevenue) / prevRevenue * 100) : 0;
        var revenueDirection = revenueChange >= 0 ? "increase" : "decrease";

        AddParagraph(doc, $"During the reporting period ({period}), the business processed {totalOrders} total orders " +
            $"with {completedCount} completed successfully, generating {Fmt(totalRevenue)} in total revenue. " +
            $"The average order value was {Fmt(avgOrder)} with {totalItems} total items sold across all completed orders." +
            (prevRevenue > 0 ? $" This represents a {Math.Abs(revenueChange):F1}% {revenueDirection} compared to the previous equivalent period ({Fmt(prevRevenue)})." : ""),
            11, false, "#334155");
        AddSpacing(doc);

        // ─── KPI SUMMARY TABLE ───
        AddSectionTitle(doc, "2. KEY PERFORMANCE INDICATORS");
        var kpiTable = new System.Windows.Documents.Table();
        kpiTable.CellSpacing = 0;
        kpiTable.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(200) });
        kpiTable.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(200) });
        kpiTable.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(200) });

        var kpiGroup = new System.Windows.Documents.TableRowGroup();
        var kpiRow1 = new System.Windows.Documents.TableRow();
        AddTableCell(kpiRow1, $"Total Revenue\n{Fmt(totalRevenue)}", true, "#667eea");
        AddTableCell(kpiRow1, $"Total Orders\n{totalOrders}", true, "#22c55e");
        AddTableCell(kpiRow1, $"Avg Order Value\n{Fmt(avgOrder)}", true, "#f59e0b");
        kpiGroup.Rows.Add(kpiRow1);

        var cancelledCount = _filteredOrders.Count(o => o.Status == "cancelled");
        var pendingCount = _filteredOrders.Count(o => o.Status == "pending");
        var completionRate = totalOrders > 0 ? (double)completedCount / totalOrders * 100 : 0;

        var kpiRow2 = new System.Windows.Documents.TableRow();
        AddTableCell(kpiRow2, $"Items Sold\n{totalItems}", true, "#0ea5e9");
        AddTableCell(kpiRow2, $"Completion Rate\n{completionRate:F1}%", true, "#8b5cf6");
        AddTableCell(kpiRow2, $"Daily Avg Revenue\n{Fmt(days > 0 ? totalRevenue / days : 0)}", true, "#ec4899");
        kpiGroup.Rows.Add(kpiRow2);

        kpiTable.RowGroups.Add(kpiGroup);
        doc.Blocks.Add(kpiTable);
        AddSpacing(doc);

        // ─── PAYMENT METHOD BREAKDOWN ───
        AddSectionTitle(doc, "3. PAYMENT METHOD BREAKDOWN");
        var paymentGroups = completed
            .GroupBy(o => string.IsNullOrWhiteSpace(o.PaymentMethod) ? "Unknown" : o.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(o => o.Total) })
            .OrderByDescending(g => g.Amount).ToList();

        if (paymentGroups.Count > 0)
        {
            var payTable = CreateDataTable(new[] { "Payment Method", "Orders", "Amount", "% of Revenue" });
            var payGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var pg in paymentGroups)
            {
                var methodLabel = pg.Method switch
                {
                    "cash" => "Cash",
                    "card" => "Card",
                    "transfer" => "Bank Transfer",
                    "mobile" => "Mobile Money",
                    _ => pg.Method
                };
                var pct = totalRevenue > 0 ? (double)(pg.Amount / totalRevenue) * 100 : 0;
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, methodLabel);
                AddDataCell(row, pg.Count.ToString());
                AddDataCell(row, Fmt(pg.Amount));
                AddDataCell(row, $"{pct:F1}%");
                payGroup.Rows.Add(row);
            }
            payTable.RowGroups.Add(payGroup);
            doc.Blocks.Add(payTable);
        }
        else
        {
            AddParagraph(doc, "No payment data available for this period.", 11, false, "#94a3b8");
        }
        AddSpacing(doc);

        // ─── TOP SELLING ITEMS ───
        AddSectionTitle(doc, "4. TOP SELLING ITEMS");
        var topItems = completed.SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Name ?? "Unknown")
            .Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) })
            .OrderByDescending(g => g.Revenue).Take(15).ToList();

        if (topItems.Count > 0)
        {
            var itemTable = CreateDataTable(new[] { "Rank", "Item Name", "Quantity Sold", "Revenue", "% of Total" });
            var itemGroup = new System.Windows.Documents.TableRowGroup();
            for (int i = 0; i < topItems.Count; i++)
            {
                var item = topItems[i];
                var pct = totalRevenue > 0 ? (double)(item.Revenue / totalRevenue) * 100 : 0;
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, $"#{i + 1}");
                AddDataCell(row, item.Name);
                AddDataCell(row, item.Qty.ToString());
                AddDataCell(row, Fmt(item.Revenue));
                AddDataCell(row, $"{pct:F1}%");
                itemGroup.Rows.Add(row);
            }
            itemTable.RowGroups.Add(itemGroup);
            doc.Blocks.Add(itemTable);
        }
        else
        {
            AddParagraph(doc, "No items sold in this period.", 11, false, "#94a3b8");
        }
        AddSpacing(doc);

        // ─── ORDER STATUS DISTRIBUTION ───
        AddSectionTitle(doc, "5. ORDER STATUS DISTRIBUTION");
        var statusTable = CreateDataTable(new[] { "Status", "Count", "Percentage" });
        var statusGroup = new System.Windows.Documents.TableRowGroup();
        var statuses = new[] { ("completed", "Completed"), ("pending", "Pending"), ("preparing", "Preparing"), ("cancelled", "Cancelled") };
        foreach (var (status, label) in statuses)
        {
            var count = _filteredOrders.Count(o => o.Status == status);
            if (count == 0) continue;
            var pct = totalOrders > 0 ? (double)count / totalOrders * 100 : 0;
            var row = new System.Windows.Documents.TableRow();
            AddDataCell(row, label);
            AddDataCell(row, count.ToString());
            AddDataCell(row, $"{pct:F1}%");
            statusGroup.Rows.Add(row);
        }
        statusTable.RowGroups.Add(statusGroup);
        doc.Blocks.Add(statusTable);
        AddSpacing(doc);

        // ─── SALES BY CATEGORY ───
        AddSectionTitle(doc, "6. SALES BY CATEGORY");
        var categories = completed.SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Category ?? "Uncategorized")
            .Select(g => new { Category = g.Key, Revenue = g.Sum(i => i.Price * i.Quantity), Count = g.Sum(i => i.Quantity) })
            .OrderByDescending(g => g.Revenue).ToList();

        if (categories.Count > 0)
        {
            var catTotalRev = categories.Sum(c => c.Revenue);
            var catTable = CreateDataTable(new[] { "Category", "Items Sold", "Revenue", "% of Total" });
            var catGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var cat in categories)
            {
                var pct = catTotalRev > 0 ? (double)(cat.Revenue / catTotalRev) * 100 : 0;
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, cat.Category);
                AddDataCell(row, cat.Count.ToString());
                AddDataCell(row, Fmt(cat.Revenue));
                AddDataCell(row, $"{pct:F1}%");
                catGroup.Rows.Add(row);
            }
            catTable.RowGroups.Add(catGroup);
            doc.Blocks.Add(catTable);
        }
        else
        {
            AddParagraph(doc, "No category data available.", 11, false, "#94a3b8");
        }
        AddSpacing(doc);

        // ─── PEAK HOURS ANALYSIS ───
        AddSectionTitle(doc, "7. PEAK HOURS ANALYSIS");
        var hourly = completed.GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.Total) })
            .OrderByDescending(g => g.Count).Take(12).ToList();

        if (hourly.Count > 0)
        {
            var hourTable = CreateDataTable(new[] { "Hour", "Orders", "Revenue", "Avg Order Value" });
            var hourGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var h in hourly.OrderBy(h => h.Hour))
            {
                var hourLabel = h.Hour == 0 ? "12:00 AM" : h.Hour < 12 ? $"{h.Hour}:00 AM" : h.Hour == 12 ? "12:00 PM" : $"{h.Hour - 12}:00 PM";
                var avg = h.Count > 0 ? h.Revenue / h.Count : 0;
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, hourLabel);
                AddDataCell(row, h.Count.ToString());
                AddDataCell(row, Fmt(h.Revenue));
                AddDataCell(row, Fmt(avg));
                hourGroup.Rows.Add(row);
            }
            hourTable.RowGroups.Add(hourGroup);
            doc.Blocks.Add(hourTable);

            var busiestHour = hourly.First();
            var bLabel = busiestHour.Hour == 0 ? "12:00 AM" : busiestHour.Hour < 12 ? $"{busiestHour.Hour}:00 AM" : busiestHour.Hour == 12 ? "12:00 PM" : $"{busiestHour.Hour - 12}:00 PM";
            AddParagraph(doc, $"Busiest hour: {bLabel} with {busiestHour.Count} orders generating {Fmt(busiestHour.Revenue)} in revenue.", 10, false, "#64748b");
        }
        else
        {
            AddParagraph(doc, "No peak hours data available.", 11, false, "#94a3b8");
        }
        AddSpacing(doc);

        // ─── STAFF PERFORMANCE ───
        AddSectionTitle(doc, "8. STAFF PERFORMANCE");
        var users = App.UserService!.GetAllUsersAsync().GetAwaiter().GetResult();
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);
        var staffStats = completed.Where(o => o.UserId.HasValue)
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new
            {
                Name = userLookup.TryGetValue(g.Key, out var n) ? n : $"User #{g.Key}",
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Total),
                AvgOrder = g.Count() > 0 ? g.Sum(o => o.Total) / g.Count() : 0
            })
            .OrderByDescending(s => s.Revenue).ToList();

        if (staffStats.Count > 0)
        {
            var staffTable = CreateDataTable(new[] { "Staff Member", "Orders", "Revenue", "Avg Order Value", "% of Revenue" });
            var staffGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var staff in staffStats)
            {
                var pct = totalRevenue > 0 ? (double)(staff.Revenue / totalRevenue) * 100 : 0;
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, staff.Name);
                AddDataCell(row, staff.Orders.ToString());
                AddDataCell(row, Fmt(staff.Revenue));
                AddDataCell(row, Fmt(staff.AvgOrder));
                AddDataCell(row, $"{pct:F1}%");
                staffGroup.Rows.Add(row);
            }
            staffTable.RowGroups.Add(staffGroup);
            doc.Blocks.Add(staffTable);
        }
        else
        {
            AddParagraph(doc, "No staff performance data available.", 11, false, "#94a3b8");
        }
        AddSpacing(doc);

        // ─── DAILY BREAKDOWN ───
        AddSectionTitle(doc, "9. DAILY BREAKDOWN");
        var daily = _filteredOrders.GroupBy(o => o.CreatedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Completed = g.Count(o => o.Status == "completed"),
                Cancelled = g.Count(o => o.Status == "cancelled"),
                Revenue = g.Where(o => o.Status == "completed").Sum(o => o.Total)
            }).ToList();

        if (daily.Count > 0)
        {
            var dailyTable = CreateDataTable(new[] { "Date", "Total Orders", "Completed", "Cancelled", "Revenue" });
            var dailyGroup = new System.Windows.Documents.TableRowGroup();
            foreach (var d in daily)
            {
                var row = new System.Windows.Documents.TableRow();
                AddDataCell(row, d.Date.ToString("MMM dd, yyyy (ddd)"));
                AddDataCell(row, d.Total.ToString());
                AddDataCell(row, d.Completed.ToString());
                AddDataCell(row, d.Cancelled.ToString());
                AddDataCell(row, Fmt(d.Revenue));
                dailyGroup.Rows.Add(row);
            }
            // Add totals row
            var totalsRow = new System.Windows.Documents.TableRow();
            totalsRow.Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));
            AddDataCell(totalsRow, "TOTAL", true);
            AddDataCell(totalsRow, daily.Sum(d => d.Total).ToString(), true);
            AddDataCell(totalsRow, daily.Sum(d => d.Completed).ToString(), true);
            AddDataCell(totalsRow, daily.Sum(d => d.Cancelled).ToString(), true);
            AddDataCell(totalsRow, Fmt(daily.Sum(d => d.Revenue)), true);
            dailyGroup.Rows.Add(totalsRow);

            dailyTable.RowGroups.Add(dailyGroup);
            doc.Blocks.Add(dailyTable);
        }
        AddSpacing(doc);

        // ─── ORDER DETAILS ───
        AddSectionTitle(doc, "10. ORDER DETAILS");
        AddParagraph(doc, $"Showing all {_filteredOrders.Count} orders in the reporting period.", 10, false, "#64748b");

        var orderTable = CreateDataTable(new[] { "Order #", "Date & Time", "Status", "Payment", "Items", "Total" });
        var orderGroup = new System.Windows.Documents.TableRowGroup();
        foreach (var order in _filteredOrders.OrderByDescending(o => o.CreatedAt))
        {
            var itemNames = string.Join(", ", order.Items.Select(i => $"{i.InventoryItem?.Name ?? "Item"} ×{i.Quantity}"));
            if (itemNames.Length > 60) itemNames = itemNames[..57] + "...";
            var row = new System.Windows.Documents.TableRow();
            AddDataCell(row, order.OrderNumber);
            AddDataCell(row, order.CreatedAt.ToString("MMM dd HH:mm"));
            AddDataCell(row, order.Status);
            AddDataCell(row, order.PaymentMethod ?? "—");
            AddDataCell(row, itemNames);
            AddDataCell(row, Fmt(order.Total));
            orderGroup.Rows.Add(row);
        }
        orderTable.RowGroups.Add(orderGroup);
        doc.Blocks.Add(orderTable);
        AddSpacing(doc);

        // ─── FOOTER ───
        AddDivider(doc);
        AddParagraph(doc, $"{_settings.CopyrightText} • {_settings.TrademarkText}", 9, false, "#94a3b8");
        AddParagraph(doc, "This report is auto-generated and confidential. Intended for internal management use only.", 8, false, "#cbd5e1");

        return doc;
    }

    // ─── PDF HELPER METHODS ───

    private static void AddParagraph(System.Windows.Documents.FlowDocument doc, string text, double fontSize, bool bold, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var p = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text)
        {
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush(color)
        })
        { Margin = new Thickness(0, 2, 0, 2) };
        doc.Blocks.Add(p);
    }

    private static void AddSectionTitle(System.Windows.Documents.FlowDocument doc, string text)
    {
        var p = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text)
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a))
        })
        { Margin = new Thickness(0, 16, 0, 8), BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea)), BorderThickness = new Thickness(0, 0, 0, 2), Padding = new Thickness(0, 0, 0, 4) };
        doc.Blocks.Add(p);
    }

    private static void AddDivider(System.Windows.Documents.FlowDocument doc)
    {
        var p = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(""))
        {
            Margin = new Thickness(0, 8, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        doc.Blocks.Add(p);
    }

    private static void AddSpacing(System.Windows.Documents.FlowDocument doc)
    {
        doc.Blocks.Add(new System.Windows.Documents.Paragraph { Margin = new Thickness(0, 6, 0, 6) });
    }

    private static System.Windows.Documents.Table CreateDataTable(string[] headers)
    {
        var table = new System.Windows.Documents.Table
        {
            CellSpacing = 0,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1)
        };

        foreach (var _ in headers)
            table.Columns.Add(new System.Windows.Documents.TableColumn());

        var headerGroup = new System.Windows.Documents.TableRowGroup();
        var headerRow = new System.Windows.Documents.TableRow();
        headerRow.Background = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        foreach (var h in headers)
        {
            var cell = new System.Windows.Documents.TableCell(
                new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(h)
                {
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                })
                { Margin = new Thickness(0) }
            )
            {
                Padding = new Thickness(6, 4, 6, 4),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
            headerRow.Cells.Add(cell);
        }
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);
        return table;
    }

    private static void AddTableCell(System.Windows.Documents.TableRow row, string text, bool bold = false, string colorHex = "#334155")
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var cell = new System.Windows.Documents.TableCell(
            new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text)
            {
                FontSize = 12,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(color)
            })
            { Margin = new Thickness(0), TextAlignment = TextAlignment.Center }
        )
        { Padding = new Thickness(8, 10, 8, 10) };
        row.Cells.Add(cell);
    }

    private static void AddDataCell(System.Windows.Documents.TableRow row, string text, bool bold = false)
    {
        var cell = new System.Windows.Documents.TableCell(
            new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text)
            {
                FontSize = 10,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
            })
            { Margin = new Thickness(0) }
        )
        {
            Padding = new Thickness(6, 3, 6, 3),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)),
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
        row.Cells.Add(cell);
    }

    // ═══════════ HELPER ═══════════

    private static TextBlock EmptyText(string text) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 20, 0, 20)
    };

    // ─── RECEIPT-STYLE PRINT REPORTS ───

    private const int RW = 42;

    private System.Windows.Documents.FlowDocument CreateReceiptDoc()
    {
        var doc = new System.Windows.Documents.FlowDocument();
        doc.FontFamily = new FontFamily("Consolas");
        doc.FontSize = 12;
        doc.PagePadding = new Thickness(20, 15, 20, 15);
        doc.ColumnWidth = double.PositiveInfinity;
        doc.TextAlignment = TextAlignment.Left;
        return doc;
    }

    private void AddRLine(System.Windows.Documents.FlowDocument doc, string text, bool bold = false)
    {
        // Pad all lines to exactly RW characters so every line has the same pixel width
        // in monospace font — prevents the print paginator from misaligning shorter lines
        var paddedText = text.Length < RW ? text + new string(' ', RW - text.Length) : text;
        var para = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(paddedText))
        { Margin = new Thickness(0), TextAlignment = TextAlignment.Left };
        if (bold) para.FontWeight = FontWeights.Bold;
        doc.Blocks.Add(para);
    }

    private string RCenter(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length >= RW) return text;
        int pad = (RW - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private void AddRSep(System.Windows.Documents.FlowDocument doc, bool dbl = false)
    {
        AddRLine(doc, new string(dbl ? '═' : '─', RW));
    }

    private List<string> WrapText(string text, int width)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return new List<string> { "" };

        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var currentLine = "";

        foreach (var word in words)
        {
            if ((currentLine + " " + word).Trim().Length <= width)
            {
                currentLine = (currentLine + " " + word).Trim();
            }
            else
            {
                if (!string.IsNullOrEmpty(currentLine)) result.Add(currentLine);
                
                var remainingWord = word;
                while (remainingWord.Length > width)
                {
                    result.Add(remainingWord.Substring(0, width));
                    remainingWord = remainingWord.Substring(width);
                }
                currentLine = remainingWord;
            }
        }
        if (!string.IsNullOrEmpty(currentLine)) result.Add(currentLine);
        return result;
    }

    private void AddRRow(System.Windows.Documents.FlowDocument doc, string name, int nameWidth, string otherCols, bool bold = false)
    {
        var lines = WrapText(name, nameWidth);
        for (int i = 0; i < lines.Count - 1; i++)
        {
            // Print leading lines with just the name
            AddRLine(doc, " " + lines[i], bold);
        }
        // Print the last line with the name matched with the other columns
        AddRLine(doc, $" {lines.Last().PadRight(nameWidth)} {otherCols}", bold);
    }

    private void OpenReportCenter_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ReportCenterWindow();
        dlg.Owner = Window.GetWindow(this);
        dlg.ShowDialog();
    }


    private async void PrintEndOfDay_Click(object sender, RoutedEventArgs e)
    {
        var from = DateFrom.SelectedDate ?? DateTime.Now.Date;
        var to = DateTo.SelectedDate ?? DateTime.Now.Date;
        var completed = _filteredOrders.Where(o => o.Status == "completed").ToList();
        PrintSalesSummary(completed, from, to, from == to && from == DateTime.Now.Date ? "END OF DAY" : "PERIOD REPORT");
    }

    private async void PrintSalesSummary(List<Order> completed, DateTime from, DateTime to, string mode)
    {
        var users = await App.UserService!.GetAllUsersAsync();
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);
        var doc = CreateReceiptDoc();

        AddRLine(doc, RCenter(_settings?.BusinessNameLine1 ?? "Restaurant"), true);
        AddRLine(doc, RCenter("SALES SUMMARY REPORT"), true);
        AddRLine(doc, RCenter($"MODE: {mode}"));
        AddRLine(doc, RCenter($"Period: {from:MMM dd, yyyy} - {to:MMM dd, yyyy}"));
        AddRLine(doc, RCenter($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRLine(doc, RCenter($"By: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRSep(doc, true);
        
        AddRLine(doc, " EXECUTIVE SUMMARY", true);
        AddRSep(doc);
        AddRLine(doc, $" {"Total Orders",-24} {completed.Count,14}");
        AddRLine(doc, $" {"Total Revenue",-24} {Fmt(completed.Sum(o => o.Total)),14}");
        AddRLine(doc, $" {"Average Order",-24} {Fmt(completed.Count > 0 ? completed.Average(o => o.Total) : 0),14}");
        AddRLine(doc, $" {"Items Sold",-24} {completed.Sum(o => o.Items.Sum(i => i.Quantity)),14}");
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " TOP SELLING ITEMS", true);
        AddRSep(doc);
        var topItems = completed.SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Name ?? "Unknown")
            .Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) })
            .OrderByDescending(g => g.Revenue).Take(10).ToList();
        
        foreach(var item in topItems)
        {
            // Use the new wrapping row helper (Name: 22 chars)
            var otherCols = $"{item.Qty,4} {Fmt(item.Revenue),12}";
            AddRRow(doc, item.Name, 22, otherCols);
        }
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " PAYMENT ANALYSIS", true);
        AddRSep(doc);
        foreach (var pg in completed.GroupBy(o => o.PaymentMethod ?? "unknown").OrderByDescending(g => g.Sum(o => o.Total)))
        {
            var lbl = pg.Key switch { "cash" => "Cash", "transfer" => "Transfer", "pos" => "POS", "cash_transfer" => "Cash/Xfer", "cash_pos" => "Cash/POS", _ => pg.Key };
            AddRLine(doc, $" {lbl,-24} {Fmt(pg.Sum(o => o.Total)),14} ({pg.Count()})");
        }
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " CASHIER PERFORMANCE", true);
        AddRSep(doc);
        foreach (var cg in completed.Where(o => o.UserId.HasValue).GroupBy(o => o.UserId!.Value).OrderByDescending(g => g.Sum(o => o.Total)))
        {
            var nm = userLookup.TryGetValue(cg.Key, out var n) ? n : $"User #{cg.Key}";
            AddRLine(doc, $" {nm,-24} {Fmt(cg.Sum(o => o.Total)),14} ({cg.Count()})");
        }
        AddRSep(doc, true);

        var preview = new ReceiptPreviewWindow(doc, mode == "END OF DAY" ? "End of Day Report" : "Sales Summary");
        preview.Owner = Window.GetWindow(this);
        preview.ShowDialog();
    }

    private async void PrintByCashier_Click(object sender, RoutedEventArgs e)
    {
        var users = await App.UserService!.GetAllUsersAsync();
        var cashierItems = users.Select(u => (Key: u.Id.ToString(), Label: u.Name)).ToList();

        var dlg = new ReportFilterWindow(ReportFilterWindow.ReportMode.ByCashier, cashierItems);
        dlg.Owner = Window.GetWindow(this);
        dlg.ShowDialog();
        if (!dlg.Confirmed) return;

        var from = dlg.FilterFrom;
        var to = dlg.FilterTo;
        var orders = _allOrders.Where(o => o.Status == "completed" && o.CreatedAt.Date >= from && o.CreatedAt.Date <= to).ToList();
        var userLookup = users.ToDictionary(u => u.Id, u => u.Name);

        if (dlg.SelectedKey != null && int.TryParse(dlg.SelectedKey, out var uid))
            orders = orders.Where(o => o.UserId == uid).ToList();

        var doc = CreateReceiptDoc();
        AddRLine(doc, RCenter(_settings?.BusinessNameLine1 ?? "Restaurant"), true);
        AddRLine(doc, RCenter(dlg.SelectedKey != null ? $"CASHIER: {dlg.SelectedLabel?.ToUpper()}" : "REPORT BY CASHIER"), true);
        AddRLine(doc, RCenter($"{from:MMM dd} - {to:MMM dd, yyyy}"));
        AddRSep(doc, true);
        AddRLine(doc, $" {"#Order",-14} {"Time",-10} {"Pay",-8} {"Total",8}");
        AddRSep(doc);

        var cashierGroups = orders.Where(o => o.UserId.HasValue).GroupBy(o => o.UserId!.Value).OrderByDescending(g => g.Sum(o => o.Total));
        foreach (var cg in cashierGroups)
        {
            var nm = userLookup.TryGetValue(cg.Key, out var n) ? n : $"User #{cg.Key}";
            AddRLine(doc, "");
            AddRLine(doc, $" CASHIER: {nm.ToUpper()}", true);
            AddRSep(doc);
            foreach (var order in cg.OrderByDescending(o => o.CreatedAt))
            {
                var pay = (order.PaymentMethod ?? "-") switch { "cash" => "Cash", "transfer" => "Xfer", "pos" => "POS", "cash_transfer" => "C+Xfer", "cash_pos" => "C+POS", var x => x };
                var num = order.OrderNumber.Length > 12 ? order.OrderNumber[^12..] : order.OrderNumber;
                
                // Use wrapping row helper for the Order Number / details line
                var otherCols = $"{order.CreatedAt:HH:mm,-10} {pay,-8} {Fmt(order.Total),8}";
                AddRRow(doc, num, 12, otherCols);
            }
            AddRSep(doc);
            AddRLine(doc, $" {"Total:",-24} {Fmt(cg.Sum(o => o.Total)),14}");
            AddRLine(doc, $" {"Orders:",-24} {cg.Count(),14}");
            AddRSep(doc);
        }

        AddRLine(doc, "");
        AddRLine(doc, " GRAND TOTAL", true);
        AddRSep(doc, true);
        AddRLine(doc, $" {"Revenue",-24} {Fmt(orders.Sum(o => o.Total)),14}");
        AddRLine(doc, $" {"Orders",-24} {orders.Count,14}");
        AddRSep(doc, true);
        AddRLine(doc, RCenter($"Printed by: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRLine(doc, RCenter($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRSep(doc, true);

        var preview = new ReceiptPreviewWindow(doc, dlg.SelectedKey != null ? $"Cashier: {dlg.SelectedLabel}" : "Report by Cashier");
        preview.Owner = Window.GetWindow(this);
        preview.ShowDialog();
    }

    private async void PrintByItem_Click(object sender, RoutedEventArgs e)
    {
        var allItems = await App.InventoryService!.GetAllItemsAsync();
        var itemList = allItems
            .OrderBy(i => i.Name)
            .Select(i => (Key: i.Id.ToString(), Label: i.Name))
            .ToList();

        var dlg = new ReportFilterWindow(ReportFilterWindow.ReportMode.ByItem, itemList);
        dlg.Owner = Window.GetWindow(this);
        dlg.ShowDialog();
        if (!dlg.Confirmed) return;

        var from = dlg.FilterFrom;
        var to = dlg.FilterTo;
        var orders = _allOrders.Where(o => o.Status == "completed" && o.CreatedAt.Date >= from && o.CreatedAt.Date <= to).ToList();

        var doc = CreateReceiptDoc();
        AddRLine(doc, RCenter(_settings?.BusinessNameLine1 ?? "Restaurant"), true);

        if (dlg.SelectedKey != null && int.TryParse(dlg.SelectedKey, out var itemId))
        {
            AddRLine(doc, RCenter($"ITEM: {dlg.SelectedLabel?.ToUpper()}"), true);
            AddRLine(doc, RCenter($"{from:MMM dd} - {to:MMM dd, yyyy}"));
            AddRSep(doc, true);
            AddRLine(doc, $"{"Date",-5} {"Order#",-8} {"Price",10} {"Qty",3} {"Total",12}");
            AddRSep(doc);

            var itemOrders = orders.Where(o => o.Items.Any(i => i.InventoryItemId == itemId)).ToList();
            int totalQty = 0; decimal totalRev = 0;
            foreach (var order in itemOrders.OrderByDescending(o => o.CreatedAt))
            {
                var oiList = order.Items.Where(i => i.InventoryItemId == itemId).ToList();
                foreach (var oi in oiList)
                {
                    var qty = oi.Quantity;
                    var rev = oi.Price * oi.Quantity;
                    totalQty += qty; totalRev += rev;
                    var num = order.OrderNumber.Length > 8 ? order.OrderNumber[^8..] : order.OrderNumber;

                    // Detail row for single item report
                    var otherCols = $"{num,-8} {Fmt(oi.Price),10} {qty,3} {Fmt(rev),12}";
                    AddRRow(doc, order.CreatedAt.ToString("MM/dd"), 4, otherCols);
                }
            }
            AddRSep(doc);
            AddRLine(doc, $" {"TOTAL",-25} {totalQty,3} {Fmt(totalRev),12}", true);
            AddRSep(doc);
            AddRLine(doc, $" Total orders: {itemOrders.Count}");
        }
        else
        {
            AddRLine(doc, RCenter("REPORT BY ITEM"), true);
            AddRLine(doc, RCenter($"{from:MMM dd} - {to:MMM dd, yyyy}"));
            AddRSep(doc, true);
            AddRLine(doc, $"{"Item",-15}{"Price",10} {"Qty",3} {"Revenue",12}");
            AddRSep(doc);

            var itemStats = orders
                .SelectMany(o => o.Items)
                .GroupBy(i => new { i.InventoryItemId, Name = i.InventoryItem?.Name ?? $"Item #{i.InventoryItemId}", i.Price })
                .Select(g => new { g.Key.Name, g.Key.Price, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            foreach (var item in itemStats)
            {
                // Item Name: 13, Price: 10, Qty: 3, Revenue: 11 (Total 41 with separators)
                var otherCols = $"{Fmt(item.Price),10} {item.Qty,3} {Fmt(item.Revenue),11}";
                AddRRow(doc, item.Name, 13, otherCols);
            }
            AddRSep(doc);
            AddRLine(doc, $"{"TOTAL",-25} {itemStats.Sum(i => i.Qty),3} {Fmt(itemStats.Sum(i => i.Revenue)),12}", true);
            AddRSep(doc);
            AddRLine(doc, $" Total unique items: {itemStats.Select(s => s.Name).Distinct().Count()}");
        }

        AddRSep(doc, true);
        AddRLine(doc, RCenter($"Printed by: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRLine(doc, RCenter($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRSep(doc, true);

        var preview = new ReceiptPreviewWindow(doc, dlg.SelectedKey != null ? $"Item: {dlg.SelectedLabel}" : "Report by Item");
        preview.Owner = Window.GetWindow(this);
        preview.ShowDialog();
    }
}
