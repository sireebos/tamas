
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RestaurantManager.Models;

namespace RestaurantManager.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadData();
    }

    private async void LoadData()
    {
        try
        {
            var user = App.CurrentUser;
            var branding = await App.BrandingService!.GetSettingsAsync();

            // Header
            var hour = DateTime.Now.Hour;
            GreetingText.Text = hour < 12 ? "Good morning," : hour < 17 ? "Good afternoon," : "Good evening,";
            UserNameText.Text = user?.Name ?? "User";
            DateText.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

            // User info
            UserRoleText.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(user?.Role ?? "—");
            UserDeptText.Text = string.IsNullOrEmpty(user?.Department) ? "—" :
                System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(user.Department);

            // Fetch data
            var orders = await App.OrderService!.GetAllOrdersAsync();
            var inventory = await App.InventoryService!.GetAllItemsAsync();
            var lowStock = await App.InventoryService!.GetLowStockItemsAsync();

            var today = DateTime.Now.Date;
            var todayOrders = orders.Where(o => o.CreatedAt.Date == today).ToList();
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var weekOrders = orders.Where(o => o.CreatedAt.Date >= weekStart).ToList();
            var monthOrders = orders.Where(o => o.CreatedAt.Date >= monthStart).ToList();

            var totalRevenue = orders.Sum(o => o.Total);
            var todayRevenue = todayOrders.Sum(o => o.Total);
            var yesterdayOrders = orders.Where(o => o.CreatedAt.Date == today.AddDays(-1)).ToList();
            var yesterdayRevenue = yesterdayOrders.Sum(o => o.Total);

            // ── Stat cards ──
            RevenueValue.Text = branding.FormatCurrency(todayRevenue);
            if (yesterdayRevenue > 0)
            {
                var pct = ((todayRevenue - yesterdayRevenue) / yesterdayRevenue) * 100;
                RevenueTrend.Text = pct >= 0 ? $"\u25B2 {pct:N0}%" : $"\u25BC {Math.Abs(pct):N0}%";
            }
            else
            {
                RevenueTrend.Text = todayRevenue > 0 ? "\u25B2 New" : "\u2014 0%";
            }

            TodayOrdersValue.Text = todayOrders.Count.ToString();
            TodayRevenueText.Text = $"{branding.FormatCurrency(todayRevenue)} today";

            var completedToday = todayOrders.Where(o => o.Status == "completed").ToList();
            TotalOrdersValue.Text = completedToday.Count.ToString();
            var avgOrderToday = completedToday.Count > 0 ? completedToday.Sum(o => o.Total) / completedToday.Count : 0;
            AvgOrderText.Text = $"Avg: {branding.FormatCurrency(avgOrderToday)}";

            InventoryValue.Text = inventory.Count.ToString();
            LowStockBadge.Text = lowStock.Count > 0 ? $"\u26A0 {lowStock.Count} low stock" : "\u2713 All stocked";

            // ── Apply card colors ──
            ApplyCardColors(branding);

            // ── Order status breakdown (Today only) ──
            PendingCount.Text = todayOrders.Count(o => o.Status == "pending").ToString();
            PreparingCount.Text = todayOrders.Count(o => o.Status == "preparing").ToString();
            CompletedCount.Text = todayOrders.Count(o => o.Status == "completed").ToString();
            CancelledCount.Text = todayOrders.Count(o => o.Status == "cancelled").ToString();

            // ── Recent orders ──
            var recent = orders.Take(8).ToList();
            RecentOrdersGrid.ItemsSource = recent.Select(o => new
            {
                o.OrderNumber,
                o.Status,
                o.Items,
                o.Total,
                FormattedTotal = branding.FormatCurrency(o.Total),
                o.CreatedAt
            }).ToList();
            OrderCountLabel.Text = $"Last {recent.Count} orders";

            // ── Low stock list ──
            if (lowStock.Count > 0)
            {
                LowStockList.ItemsSource = lowStock;
                LowStockList.Visibility = Visibility.Visible;
                NoLowStockText.Visibility = Visibility.Collapsed;
            }
            else
            {
                LowStockList.Visibility = Visibility.Collapsed;
                NoLowStockText.Visibility = Visibility.Visible;
            }

            // ── Today's summary ──
            TodayCompletedText.Text = todayOrders.Count(o => o.Status == "completed").ToString();
            TodayPendingText.Text = todayOrders.Count(o => o.Status == "pending").ToString();
            var todayAvg = todayOrders.Count > 0 ? todayRevenue / todayOrders.Count : 0;
            TodayAvgText.Text = branding.FormatCurrency(todayAvg);

            // ── Performance ──
            WeekOrdersText.Text = $"{weekOrders.Count} orders";
            MonthOrdersText.Text = $"{monthOrders.Count} orders";
            MonthRevenueText.Text = branding.FormatCurrency(monthOrders.Sum(o => o.Total));
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Error loading dashboard");
            MessageBox.Show($"Error loading dashboard: {ex.Message}");
        }
    }

    private void NewOrder_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to orders view
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ContentArea.Content = new OrdersView();
        }
    }

    private void ViewOrders_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ContentArea.Content = new OrdersView();
        }
    }

    private void ViewInventory_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
        {
            mw.ContentArea.Content = new InventoryView();
        }
    }

    private void RefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private Color ParseHex(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void ApplyCardColors(BrandingSettings b)
    {
        DashCard1Stop1.Color = ParseHex(b.Card1Color1, Color.FromRgb(0x66, 0x7e, 0xea));
        DashCard1Stop2.Color = ParseHex(b.Card1Color2, Color.FromRgb(0x76, 0x4b, 0xa2));
        DashCard2Stop1.Color = ParseHex(b.Card2Color1, Color.FromRgb(0xf9, 0x73, 0x16));
        DashCard2Stop2.Color = ParseHex(b.Card2Color2, Color.FromRgb(0xef, 0x44, 0x44));
        DashCard3Stop1.Color = ParseHex(b.Card3Color1, Color.FromRgb(0x10, 0xb9, 0x81));
        DashCard3Stop2.Color = ParseHex(b.Card3Color2, Color.FromRgb(0x05, 0x96, 0x69));
        DashCard4Stop1.Color = ParseHex(b.Card4Color1, Color.FromRgb(0x8b, 0x5c, 0xf6));
        DashCard4Stop2.Color = ParseHex(b.Card4Color2, Color.FromRgb(0x6d, 0x28, 0xd9));

        // Apply card text color
        var textColor = ParseHex(b.CardTextColor, Colors.White);
        var textBrush = new SolidColorBrush(textColor);
        var subBrush = new SolidColorBrush(Color.FromArgb(0xCC, textColor.R, textColor.G, textColor.B));
        RevenueValue.Foreground = textBrush;
        TodayOrdersValue.Foreground = textBrush;
        TotalOrdersValue.Foreground = textBrush;
        InventoryValue.Foreground = textBrush;
        DashCard1Label.Foreground = subBrush;
        DashCard1Sub.Foreground = subBrush;
        RevenueTrend.Foreground = subBrush;
        DashCard2Label.Foreground = subBrush;
        TodayRevenueText.Foreground = subBrush;
        DashCard3Label.Foreground = subBrush;
        AvgOrderText.Foreground = subBrush;
        DashCard4Label.Foreground = subBrush;
        LowStockBadge.Foreground = subBrush;
    }
    public void ShortcutPrintEndOfDay()
    {
        var win = new ReportCenterWindow();
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();
    }
}
