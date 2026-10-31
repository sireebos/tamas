using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RestaurantManager.Models;

namespace RestaurantManager.Views;

public partial class ReportCenterWindow : Window
{
    private const int RW = 42;

    private string _activeTab = "endofday";
    private string _activeFilter = "today";
    private DateTime _filterFrom;
    private DateTime _filterTo;

    private List<Order> _allOrders = new();
    private List<User> _allUsers = new();
    private List<InventoryItem> _allItems = new();
    private List<(string Key, string Label)> _itemList = new();
    private BrandingSettings _settings = new();

    public ReportCenterWindow()
    {
        InitializeComponent();
        _filterFrom = DateTime.Now.Date;
        _filterTo = DateTime.Now.Date;
        DateFromPicker.SelectedDate = _filterFrom;
        DateToPicker.SelectedDate = _filterTo;
        HighlightDateBtn("today");
        UpdatePeriodLabel();
        Loaded += async (_, _) => await LoadDataAsync();
        SetActiveTab("endofday");
    }

    private async System.Threading.Tasks.Task LoadDataAsync()
    {
        try
        {
            _settings = await App.BrandingService!.GetSettingsAsync();
            _allUsers = await App.UserService!.GetAllUsersAsync();
            _allOrders = await App.OrderService!.GetAllOrdersAsync();
            _allItems = await App.InventoryService!.GetAllItemsAsync();

            // Populate cashier combo
            CashierCombo.Items.Clear();
            CashierCombo.Items.Add(new ComboBoxItem { Content = "📋 All Cashiers", Tag = "__all__" });
            foreach (var u in _allUsers.OrderBy(u => u.Name))
                CashierCombo.Items.Add(new ComboBoxItem { Content = u.Name, Tag = u.Id.ToString() });
            CashierCombo.SelectedIndex = 0;

            // Populate item list
            _itemList = _allItems.OrderBy(i => i.Name).Select(i => (Key: i.Id.ToString(), Label: i.Name)).ToList();
            PopulateItemList("");
        }
        catch (Exception ex)
        {
            App.Logger?.LogWarning(ex, "Failed to load report data");
        }
    }

    // ─── TAB SWITCHING ───

    private void SetActiveTab(string tab)
    {
        _activeTab = tab;
        var activeBg = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var activeFg = Brushes.White;
        var normalBg = Brushes.Transparent;
        var normalFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));
        var activeBorder = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var normalBorder = Brushes.Transparent;

        foreach (var b in new[] { TabEndOfDay, TabByCashier, TabByItem })
        {
            bool active = (b == TabEndOfDay && tab == "endofday") ||
                           (b == TabByCashier && tab == "bycashier") ||
                           (b == TabByItem && tab == "byitem");
            b.Background = normalBg;
            b.Foreground = active ? activeBg : normalFg;
            b.BorderBrush = active ? activeBorder : normalBorder;
        }

        // Show/hide sections
        EndOfDayInfo.Visibility = tab == "endofday" ? Visibility.Visible : Visibility.Collapsed;
        CashierSection.Visibility = (tab == "bycashier" || tab == "byitem") ? Visibility.Visible : Visibility.Collapsed;
        ItemSection.Visibility = tab == "byitem" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TabEndOfDay_Click(object s, RoutedEventArgs e) => SetActiveTab("endofday");
    private void TabByCashier_Click(object s, RoutedEventArgs e) => SetActiveTab("bycashier");
    private void TabByItem_Click(object s, RoutedEventArgs e) => SetActiveTab("byitem");

    // ─── DATE FILTER ───

    private void DateFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string filter) return;
        _activeFilter = filter;
        var today = DateTime.Now.Date;
        switch (filter)
        {
            case "yesterday": _filterFrom = today.AddDays(-1); _filterTo = today.AddDays(-1); break;
            case "today": _filterFrom = today; _filterTo = today; break;
            case "week": _filterFrom = today.AddDays(-(int)today.DayOfWeek); _filterTo = today; break;
            case "month": _filterFrom = new DateTime(today.Year, today.Month, 1); _filterTo = today; break;
            case "custom": break;
        }
        CustomDatePanel.Visibility = filter == "custom" ? Visibility.Visible : Visibility.Collapsed;
        if (filter != "custom")
        {
            DateFromPicker.SelectedDate = _filterFrom;
            DateToPicker.SelectedDate = _filterTo;
        }
        HighlightDateBtn(filter);
        UpdatePeriodLabel();
    }

    private void CustomDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (DateFromPicker.SelectedDate.HasValue) _filterFrom = DateFromPicker.SelectedDate.Value;
        if (DateToPicker.SelectedDate.HasValue) _filterTo = DateToPicker.SelectedDate.Value;
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
            "yesterday" => $"Yesterday — {_filterFrom:MMM dd, yyyy}",
            "today" => $"Today — {_filterFrom:MMM dd, yyyy}",
            "week" => $"This Week — {_filterFrom:MMM dd} to {_filterTo:MMM dd, yyyy}",
            "month" => $"This Month — {_filterFrom:MMMM yyyy}",
            _ => $"{_filterFrom:MMM dd, yyyy} — {_filterTo:MMM dd, yyyy}"
        };
    }

    // ─── CASHIER & ITEM SELECTORS ───

    private void CashierCombo_Changed(object sender, SelectionChangedEventArgs e) { }

    private void PopulateItemList(string search)
    {
        ItemList.Items.Clear();
        ItemList.Items.Add(new ListBoxItem { Content = "📋 All Items", Tag = "__all__", FontWeight = FontWeights.SemiBold });

        var filtered = string.IsNullOrWhiteSpace(search)
            ? _itemList
            : _itemList.Where(i => i.Label.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var (key, label) in filtered)
            ItemList.Items.Add(new ListBoxItem { Content = label, Tag = key });

        if (ItemList.Items.Count > 0)
            ItemList.SelectedIndex = 0;
    }

    private void ItemSearch_Changed(object sender, TextChangedEventArgs e)
    {
        PopulateItemList(ItemSearchBox.Text.Trim());
    }

    private void ItemList_Changed(object sender, SelectionChangedEventArgs e) { }

    // ─── HELPERS ───

    private string Fmt(decimal amount) => _settings.FormatCurrency(amount);

    private FlowDocument CreateReceiptDoc()
    {
        var doc = new FlowDocument();
        doc.FontFamily = new FontFamily("Consolas");
        doc.FontSize = 12;
        doc.PagePadding = new Thickness(20, 15, 20, 15);
        doc.ColumnWidth = double.PositiveInfinity;
        doc.TextAlignment = TextAlignment.Left;
        return doc;
    }

    private void AddRLine(FlowDocument doc, string text, bool bold = false)
    {
        var paddedText = text.Length < RW ? text + new string(' ', RW - text.Length) : text;
        var para = new Paragraph(new Run(paddedText))
        { Margin = new Thickness(0), TextAlignment = TextAlignment.Left };
        if (bold) para.FontWeight = FontWeights.Bold;
        doc.Blocks.Add(para);
    }

    private string CT(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length >= RW) return text;
        int pad = (RW - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private void AddRSep(FlowDocument doc, bool dbl = false)
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

    private void AddRRow(FlowDocument doc, string name, int nameWidth, string otherCols, bool bold = false)
    {
        var lines = WrapText(name, nameWidth);
        for (int i = 0; i < lines.Count - 1; i++)
        {
            AddRLine(doc, " " + lines[i], bold);
        }
        AddRLine(doc, $" {lines.Last().PadRight(nameWidth)} {otherCols}", bold);
    }

    // ─── PRINT ───

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var orders = _allOrders
            .Where(o => o.Status == "completed" && o.CreatedAt.Date >= _filterFrom && o.CreatedAt.Date <= _filterTo)
            .ToList();

        // Filter by selected cashier if applicable
        int? selectedCashierId = null;
        string? selectedCashierName = null;
        if ((_activeTab == "bycashier" || _activeTab == "byitem") && CashierCombo.SelectedItem is ComboBoxItem ci)
        {
            var tag = ci.Tag?.ToString();
            if (tag != "__all__" && int.TryParse(tag, out var uid))
            {
                selectedCashierId = uid;
                selectedCashierName = ci.Content?.ToString();
                orders = orders.Where(o => o.UserId == uid).ToList();
            }
        }

        var userLookup = _allUsers.ToDictionary(u => u.Id, u => u.Name);

        switch (_activeTab)
        {
            case "endofday":
                PrintEndOfDay(orders, userLookup);
                break;
            case "bycashier":
                PrintByCashier(orders, userLookup, selectedCashierId, selectedCashierName);
                break;
            case "byitem":
                PrintByItem(orders, selectedCashierId, selectedCashierName);
                break;
        }
    }

    private void PrintEndOfDay(List<Order> orders, Dictionary<int, string> userLookup)
    {
        var doc = CreateReceiptDoc();
        bool isToday = _filterFrom == _filterTo && _filterFrom == DateTime.Now.Date;

        AddRLine(doc, CT(_settings?.BusinessNameLine1 ?? "Restaurant"), true);
        AddRLine(doc, CT(isToday ? "END OF DAY REPORT" : "SALES ANALYSIS REPORT"), true);
        AddRLine(doc, CT($"Period: {_filterFrom:MMM dd, yyyy} - {_filterTo:MMM dd, yyyy}"));
        AddRLine(doc, CT($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRLine(doc, CT($"By: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRSep(doc, true);

        AddRLine(doc, " SALES SUMMARY", true);
        AddRSep(doc);
        AddRLine(doc, $" {"Total Orders",-24} {orders.Count,14}");
        AddRLine(doc, $" {"Total Revenue",-24} {Fmt(orders.Sum(o => o.Total)),14}");
        AddRLine(doc, $" {"Avg Order",-24} {Fmt(orders.Count > 0 ? orders.Average(o => o.Total) : 0),14}");
        AddRLine(doc, $" {"Items Sold",-24} {orders.Sum(o => o.Items.Sum(i => i.Quantity)),14}");
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " TOP SELLING ITEMS", true);
        AddRSep(doc);
        var topItems = orders.SelectMany(o => o.Items)
            .GroupBy(i => i.InventoryItem?.Name ?? "Unknown")
            .Select(g => new { Name = g.Key, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) })
            .OrderByDescending(g => g.Revenue).Take(10).ToList();
        
        foreach(var item in topItems)
        {
            var otherCols = $"{item.Qty,4} {Fmt(item.Revenue),12}";
            AddRRow(doc, item.Name, 22, otherCols);
        }
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " PAYMENT METHODS", true);
        AddRSep(doc);
        foreach (var pg in orders.GroupBy(o => o.PaymentMethod ?? "unknown").OrderByDescending(g => g.Sum(o => o.Total)))
        {
            var lbl = pg.Key switch { "cash" => "Cash", "transfer" => "Transfer", "pos" => "POS", "cash_transfer" => "Cash/Xfer", "cash_pos" => "Cash/POS", _ => pg.Key };
            AddRLine(doc, $" {lbl,-24} {Fmt(pg.Sum(o => o.Total)),14} ({pg.Count()})");
        }
        AddRSep(doc);
        AddRLine(doc, "");

        AddRLine(doc, " SALES BY CASHIER", true);
        AddRSep(doc);
        foreach (var cg in orders.Where(o => o.UserId.HasValue).GroupBy(o => o.UserId!.Value).OrderByDescending(g => g.Sum(o => o.Total)))
        {
            var nm = userLookup.TryGetValue(cg.Key, out var n) ? n : $"User #{cg.Key}";
            AddRLine(doc, $" {nm,-24} {Fmt(cg.Sum(o => o.Total)),14} ({cg.Count()})");
        }
        AddRSep(doc, true);

        ShowPreview(doc, isToday ? "End of Day Report" : "Sales Report");
    }

    private void PrintByCashier(List<Order> orders, Dictionary<int, string> userLookup, int? cashierId, string? cashierName)
    {
        var doc = CreateReceiptDoc();
        AddRLine(doc, CT(_settings?.BusinessNameLine1 ?? "Restaurant"), true);
        AddRLine(doc, CT(cashierId.HasValue ? $"CASHIER: {cashierName?.ToUpper()}" : "REPORT BY CASHIER"), true);
        AddRLine(doc, CT($"{_filterFrom:MMM dd} - {_filterTo:MMM dd, yyyy}"));
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
        AddRLine(doc, CT($"By: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRLine(doc, CT($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRSep(doc, true);

        ShowPreview(doc, cashierId.HasValue ? $"Cashier: {cashierName}" : "Report by Cashier");
    }

    private void PrintByItem(List<Order> orders, int? cashierId, string? cashierName)
    {
        var doc = CreateReceiptDoc();
        AddRLine(doc, CT(_settings?.BusinessNameLine1 ?? "Restaurant"), true);

        // Get selected item
        string? selectedItemKey = null;
        string? selectedItemLabel = null;
        if (ItemList.SelectedItem is ListBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag != "__all__")
            {
                selectedItemKey = tag;
                selectedItemLabel = item.Content?.ToString();
            }
        }

        if (selectedItemKey != null && int.TryParse(selectedItemKey, out var itemId))
        {
            var title = $"ITEM: {selectedItemLabel?.ToUpper()}";
            if (cashierId.HasValue) title += $" (Cashier: {cashierName})";
            AddRLine(doc, CT(title), true);
            AddRLine(doc, CT($"{_filterFrom:MMM dd} - {_filterTo:MMM dd, yyyy}"));
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
            var title = "REPORT BY ITEM";
            if (cashierId.HasValue) title += $" (Cashier: {cashierName})";
            AddRLine(doc, CT(title), true);
            AddRLine(doc, CT($"{_filterFrom:MMM dd} - {_filterTo:MMM dd, yyyy}"));
            AddRSep(doc, true);
            AddRLine(doc, $" {"Item",-14} {"Price",10} {"Qty",3} {"Revenue",11}");
            AddRSep(doc);

            var itemStatsQuery = orders
                .SelectMany(o => o.Items)
                .GroupBy(i => new { i.InventoryItemId, Name = i.InventoryItem?.Name ?? $"Item #{i.InventoryItemId}", i.Price })
                .Select(g => new { g.Key.Name, g.Key.Price, Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.Price * i.Quantity) });

            string sort = (ItemSortCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "az";
            itemStatsQuery = sort switch
            {
                "az" => itemStatsQuery.OrderBy(x => x.Name),
                "za" => itemStatsQuery.OrderByDescending(x => x.Name),
                "rev_desc" => itemStatsQuery.OrderByDescending(x => x.Revenue),
                "rev_asc" => itemStatsQuery.OrderBy(x => x.Revenue),
                _ => itemStatsQuery.OrderBy(x => x.Name)
            };

            var itemStats = itemStatsQuery.ToList();

            foreach (var it in itemStats)
            {
                var otherCols = $"{Fmt(it.Price),10} {it.Qty,3} {Fmt(it.Revenue),11}";
                AddRRow(doc, it.Name, 14, otherCols);
            }
            AddRSep(doc);
            AddRLine(doc, $"{"TOTAL",-25} {itemStats.Sum(i => i.Qty),3} {Fmt(itemStats.Sum(i => i.Revenue)),12}", true);
            AddRSep(doc);
            AddRLine(doc, $" Total unique items: {itemStats.Select(s => s.Name).Distinct().Count()}");
        }

        AddRSep(doc, true);
        AddRLine(doc, CT($"By: {App.CurrentUser?.Name ?? "Admin"}"));
        AddRLine(doc, CT($"Generated: {DateTime.Now:MMM dd, yyyy HH:mm}"));
        AddRSep(doc, true);

        ShowPreview(doc, selectedItemKey != null ? $"Item: {selectedItemLabel}" : "Report by Item");
    }

    private void ShowPreview(FlowDocument doc, string title)
    {
        var preview = new ReceiptPreviewWindow(doc, title);
        preview.Owner = this;
        preview.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
