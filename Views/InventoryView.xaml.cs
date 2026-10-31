
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RestaurantManager.Models;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace RestaurantManager.Views;

public class InventoryDisplayItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public int Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal CostPrice { get; set; }
    public int MinLevel { get; set; }
    public int ReorderQuantity { get; set; }
    public string FormattedCost { get; set; } = "";
    public string FormattedValue { get; set; } = "";
    public string StatusText { get; set; } = "In Stock";
    public SolidColorBrush StatusForeground { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
    public SolidColorBrush StatusBackground { get; set; } = new SolidColorBrush(Color.FromRgb(0xf0, 0xfd, 0xf4));
    public double StockBarWidth { get; set; } = 90;
    public SolidColorBrush StockBarBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
    public string CategoryIcon { get; set; } = "🏷";
    public bool IsSelected { get; set; }
}

public partial class InventoryView : UserControl
{
    private List<InventoryItem> _allItems = new();
    private List<Category> _categories = new();
    private int? _editingItemId;
    private int? _adjustingItemId;
    private BrandingSettings? _brandingSettings;

    public InventoryView()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadInventory();
    }

    private async void LoadInventory()
    {
        try
        {
            _allItems = await App.InventoryService!.GetAllItemsAsync();
            _brandingSettings = await App.BrandingService!.GetSettingsAsync();
            _categories = await App.CategoryService!.GetAllAsync();
            UpdateSummaryCards();
            PopulateCategoryFilter();
            PopulateDlgCategories();
            ApplyFilters();
            ApplyCardColors();
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Error loading inventory");
            MessageBox.Show($"Error loading inventory: {ex.Message}");
        }
    }

    private string FormatCurrency(decimal amount)
    {
        return _brandingSettings?.FormatCurrency(amount) ?? $"R {amount:N2}";
    }

    private void UpdateSummaryCards()
    {
        TotalItemsText.Text = _allItems.Count.ToString();
        var categories = _allItems.Where(i => i.Category != null).Select(i => i.Category).Distinct().Count();
        TotalCategoriesText.Text = $"{categories} categories";

        var lowStock = _allItems.Count(i => i.Quantity > 0 && i.Quantity < i.MinLevel);
        LowStockText.Text = lowStock.ToString();

        var outOfStock = _allItems.Count(i => i.Quantity == 0);
        OutOfStockText.Text = outOfStock.ToString();

        var totalValue = _allItems.Sum(i => i.Quantity * i.CostPrice);
        StockValueText.Text = FormatCurrency(totalValue);

        // Update cost label in dialog
        var symbol = _brandingSettings?.CurrencySymbol ?? "R";
        DlgCostLabel.Text = $"Cost Price ({symbol})";
    }

    private Color ParseHex(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private void ApplyCardColors()
    {
        if (_brandingSettings == null) return;
        InvCard1Stop1.Color = ParseHex(_brandingSettings.Card1Color1, Color.FromRgb(0x66, 0x7e, 0xea));
        InvCard1Stop2.Color = ParseHex(_brandingSettings.Card1Color2, Color.FromRgb(0x76, 0x4b, 0xa2));
        InvCard2Stop1.Color = ParseHex(_brandingSettings.Card2Color1, Color.FromRgb(0xf9, 0x73, 0x16));
        InvCard2Stop2.Color = ParseHex(_brandingSettings.Card2Color2, Color.FromRgb(0xea, 0x58, 0x0c));
        InvCard3Stop1.Color = ParseHex(_brandingSettings.Card3Color1, Color.FromRgb(0xef, 0x44, 0x44));
        InvCard3Stop2.Color = ParseHex(_brandingSettings.Card3Color2, Color.FromRgb(0xdc, 0x26, 0x26));
        InvCard4Stop1.Color = ParseHex(_brandingSettings.Card4Color1, Color.FromRgb(0x22, 0xc5, 0x5e));
        InvCard4Stop2.Color = ParseHex(_brandingSettings.Card4Color2, Color.FromRgb(0x16, 0xa3, 0x4a));

        // Apply card text color
        var textColor = ParseHex(_brandingSettings.CardTextColor, Colors.White);
        var textBrush = new SolidColorBrush(textColor);
        var subBrush = new SolidColorBrush(Color.FromArgb(0xCC, textColor.R, textColor.G, textColor.B));
        TotalItemsText.Foreground = textBrush;
        LowStockText.Foreground = textBrush;
        OutOfStockText.Foreground = textBrush;
        StockValueText.Foreground = textBrush;
        InvCard1Label.Foreground = subBrush;
        TotalCategoriesText.Foreground = subBrush;
        InvCard2Label.Foreground = subBrush;
        InvCard2Sub.Foreground = subBrush;
        InvCard3Label.Foreground = subBrush;
        InvCard3Sub.Foreground = subBrush;
        InvCard4Label.Foreground = subBrush;
        InvCard4Sub.Foreground = subBrush;
    }

    private void PopulateCategoryFilter()
    {
        var selected = CategoryFilter.SelectedIndex;
        CategoryFilter.Items.Clear();
        CategoryFilter.Items.Add(new ComboBoxItem { Content = "All Categories", IsSelected = true });
        foreach (var cat in _allItems.Where(i => i.Category != null).Select(i => i.Category).Distinct().OrderBy(c => c))
        {
            CategoryFilter.Items.Add(new ComboBoxItem { Content = cat });
        }
        CategoryFilter.SelectedIndex = selected >= 0 && selected < CategoryFilter.Items.Count ? selected : 0;
    }

    private void PopulateDlgCategories()
    {
        DlgCategory.Items.Clear();
        foreach (var cat in _categories)
            DlgCategory.Items.Add(cat.Name);
    }


    private void ApplyFilters()
    {
        if (InventoryGrid == null || SearchBox == null) return;

        var items = _allItems.AsEnumerable();

        // Search
        var search = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            items = items.Where(i =>
                i.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.Category?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Category
        if (CategoryFilter.SelectedIndex > 0 && CategoryFilter.SelectedItem is ComboBoxItem catItem)
        {
            var cat = catItem.Content?.ToString();
            items = items.Where(i => i.Category == cat);
        }

        // Status
        items = StatusFilter.SelectedIndex switch
        {
            1 => items.Where(i => i.Quantity > 0 && i.Quantity < i.MinLevel),
            2 => items.Where(i => i.Quantity == 0),
            3 => items.Where(i => i.Quantity >= i.MinLevel),
            _ => items
        };

        var result = items.ToList();
        var displayItems = result.Select(i =>
        {
            string statusText;
            Color statusFg, statusBgColor, barColor;
            if (i.Quantity == 0)
            {
                statusText = "Out of Stock";
                statusFg = Color.FromRgb(0xef, 0x44, 0x44);
                statusBgColor = Color.FromRgb(0xfe, 0xf2, 0xf2);
                barColor = Color.FromRgb(0xef, 0x44, 0x44);
            }
            else if (i.Quantity < i.MinLevel)
            {
                statusText = "Low Stock";
                statusFg = Color.FromRgb(0xf9, 0x73, 0x16);
                statusBgColor = Color.FromRgb(0xff, 0xf7, 0xed);
                barColor = Color.FromRgb(0xf9, 0x73, 0x16);
            }
            else
            {
                statusText = "In Stock";
                statusFg = Color.FromRgb(0x22, 0xc5, 0x5e);
                statusBgColor = Color.FromRgb(0xf0, 0xfd, 0xf4);
                barColor = Color.FromRgb(0x22, 0xc5, 0x5e);
            }

            var pct = i.MinLevel > 0 ? Math.Min(1.0, (double)i.Quantity / i.MinLevel) : (i.Quantity > 0 ? 1.0 : 0.0);
            var barWidth = Math.Max(2, pct * 90.0);

            var icon = (i.Category?.ToLower()) switch
            {
                "proteins" or "meat" => "🥩",
                "dairy" => "🧀",
                "produce" or "vegetables" or "fruits" => "🥬",
                "grains" or "dry goods" => "🌾",
                "beverages" or "drinks" => "🥤",
                "spices" or "seasonings" => "🧂",
                "supplies" or "cleaning" => "🧹",
                _ => "🏷"
            };

            return new InventoryDisplayItem
            {
                Id = i.Id,
                Name = i.Name,
                Description = i.Description,
                Category = i.Category,
                SubCategory = i.SubCategory,
                Quantity = i.Quantity,
                Unit = i.Unit,
                CostPrice = i.CostPrice,
                MinLevel = i.MinLevel,
                ReorderQuantity = i.ReorderQuantity,
                FormattedCost = FormatCurrency(i.CostPrice),
                FormattedValue = FormatCurrency(i.StockValue),
                StatusText = statusText,
                StatusForeground = new SolidColorBrush(statusFg),
                StatusBackground = new SolidColorBrush(statusBgColor),
                StockBarWidth = barWidth,
                StockBarBrush = new SolidColorBrush(barColor),
                CategoryIcon = icon
            };
        }).ToList();
        InventoryGrid.ItemsSource = displayItems;
        ResultCountText.Text = $"{result.Count} items";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void CategoryFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void RefreshBtn_Click(object sender, RoutedEventArgs e) => LoadInventory();

    // ─── MULTI-SELECT ───

    private void SelectAll_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && InventoryGrid.ItemsSource is List<InventoryDisplayItem> items)
        {
            var isChecked = cb.IsChecked == true;
            foreach (var item in items)
                item.IsSelected = isChecked;
            InventoryGrid.Items.Refresh();
            UpdateSelectionBar();
        }
    }

    private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSelectionBar();
    }

    private void UpdateSelectionBar()
    {
        if (InventoryGrid.ItemsSource is not List<InventoryDisplayItem> items) return;
        var count = items.Count(i => i.IsSelected);
        if (count > 0)
        {
            SelectionBar.Visibility = Visibility.Visible;
            SelectedCountText.Text = $"{count} item{(count > 1 ? "s" : "")} selected";
        }
        else
        {
            SelectionBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.ItemsSource is not List<InventoryDisplayItem> items) return;
        var selected = items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Bulk Delete",
            $"Delete {selected.Count} selected item{(selected.Count > 1 ? "s" : "")}?\nThis action cannot be undone.");

        if (!confirmed) return;

        try
        {
            foreach (var item in selected)
                await App.InventoryService!.DeleteItemAsync(item.Id);
            LoadInventory();
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Error deleting inventory items");
            await App.Notifications!.ShowErrorAsync("Error", ex.Message);
        }
    }

    private void CancelSelection_Click(object sender, RoutedEventArgs e)
    {
        if (InventoryGrid.ItemsSource is List<InventoryDisplayItem> items)
        {
            foreach (var item in items)
                item.IsSelected = false;
            InventoryGrid.Items.Refresh();
        }
        SelectionBar.Visibility = Visibility.Collapsed;
    }

    // ─── ADD / EDIT DIALOG ───

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        _editingItemId = null;
        DialogTitle.Text = "Add New Item";
        DlgSaveBtn.Content = "Add Item";
        ClearDialog();
        DialogOverlay.Visibility = Visibility.Visible;
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _allItems.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            _editingItemId = id;
            DialogTitle.Text = "Edit Item";
            DlgSaveBtn.Content = "Save Changes";
            DlgName.Text = item.Name;
            DlgCategory.Text = item.Category ?? "";
            _ = UpdateDlgSubCategories(item.Category);
            DlgSubCategory.Text = item.SubCategory ?? "";
            DlgUnit.Text = item.Unit ?? "";
            DlgQuantity.Text = item.Quantity.ToString();
            DlgCostPrice.Text = item.CostPrice.ToString("N2");
            DlgMinLevel.Text = item.MinLevel.ToString();
            DlgReorderQty.Text = item.ReorderQuantity.ToString();
            DlgDescription.Text = item.Description ?? "";
            DlgStatus.Text = "";
            DialogOverlay.Visibility = Visibility.Visible;
        }
    }

    private void DlgCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DlgCategory.SelectedItem is string cat)
            _ = UpdateDlgSubCategories(cat);
    }

    private void DlgCategory_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = UpdateDlgSubCategories(DlgCategory.Text);
    }

    private async Task UpdateDlgSubCategories(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            DlgSubCategory.Items.Clear();
            return;
        }

        // 1. Get from formal sub-categories table
        var cats = await App.CategoryService!.GetAllAsync();
        var cat = cats.FirstOrDefault(c => c.Name == category);
        var subNames = new List<string>();
        
        if (cat != null)
        {
            var formalSubs = await App.SubCategoryService!.GetByCategoryAsync(cat.Id);
            subNames.AddRange(formalSubs.Select(s => s.Name));
        }

        // 2. Add sub-categories from existing items (legacy or ad-hoc)
        var itemSubs = _allItems
            .Where(i => i.Category == category && !string.IsNullOrEmpty(i.SubCategory))
            .Select(i => i.SubCategory!)
            .Distinct();
        
        foreach (var sub in itemSubs)
        {
            if (!subNames.Contains(sub, StringComparer.OrdinalIgnoreCase))
                subNames.Add(sub);
        }

        subNames = subNames.OrderBy(s => s).ToList();

        DlgSubCategory.Items.Clear();
        foreach (var sub in subNames)
            DlgSubCategory.Items.Add(sub);
    }

    private void ClearDialog()
    {
        DlgName.Text = DlgCategory.Text = DlgSubCategory.Text = DlgUnit.Text = DlgQuantity.Text = "";
        DlgCostPrice.Text = DlgMinLevel.Text = DlgReorderQty.Text = DlgDescription.Text = "";
        DlgStatus.Text = "";
    }

    private void CloseDialog_Click(object sender, RoutedEventArgs e)
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SaveItem_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DlgName.Text))
        {
            DlgStatus.Text = "Item name is required.";
            return;
        }
        if (!int.TryParse(DlgQuantity.Text, out var qty) || qty < 0)
        {
            DlgStatus.Text = "Enter a valid quantity.";
            return;
        }
        if (!int.TryParse(DlgMinLevel.Text, out var minLvl)) minLvl = 0;
        if (!int.TryParse(DlgReorderQty.Text, out var reorderQty)) reorderQty = 0;
        if (!decimal.TryParse(DlgCostPrice.Text, out var cost)) cost = 0;

        try
        {
            DlgSaveBtn.IsEnabled = false;

            var item = new InventoryItem
            {
                Name = DlgName.Text.Trim(),
                Category = string.IsNullOrWhiteSpace(DlgCategory.Text) ? null : DlgCategory.Text.Trim(),
                SubCategory = string.IsNullOrWhiteSpace(DlgSubCategory.Text) ? null : DlgSubCategory.Text.Trim(),
                Unit = string.IsNullOrWhiteSpace(DlgUnit.Text) ? null : DlgUnit.Text.Trim(),
                Quantity = qty,
                CostPrice = cost,
                MinLevel = minLvl,
                ReorderQuantity = reorderQty,
                Description = string.IsNullOrWhiteSpace(DlgDescription.Text) ? null : DlgDescription.Text.Trim()
            };

            if (_editingItemId.HasValue)
            {
                item.Id = _editingItemId.Value;
                await App.InventoryService!.UpdateItemAsync(item);
            }
            else
            {
                await App.InventoryService!.CreateItemAsync(item);
            }

            DialogOverlay.Visibility = Visibility.Collapsed;
            LoadInventory();
        }
        catch (Exception ex)
        {
            DlgStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            DlgSaveBtn.IsEnabled = true;
        }
    }

    // ─── STOCK ADJUST ───

    private void AdjustStock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _allItems.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            _adjustingItemId = id;
            AdjustItemName.Text = $"{item.Name} ({item.Unit})";
            AdjustCurrentQty.Text = item.Quantity.ToString();
            AdjustNewQty.Text = "0";
            AdjustOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CloseAdjust_Click(object sender, RoutedEventArgs e)
    {
        AdjustOverlay.Visibility = Visibility.Collapsed;
    }

    private async void ConfirmAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (!_adjustingItemId.HasValue) return;
        if (!int.TryParse(AdjustNewQty.Text, out var addQty) || addQty < 0)
        {
            await App.Notifications!.ShowWarningAsync("Invalid Input", "Enter a valid quantity to add.");
            return;
        }

        try
        {
            var currentItem = _allItems.FirstOrDefault(i => i.Id == _adjustingItemId.Value);
            var totalQty = (currentItem?.Quantity ?? 0) + addQty;
            await App.InventoryService!.UpdateQuantityAsync(_adjustingItemId.Value, totalQty);
            AdjustOverlay.Visibility = Visibility.Collapsed;
            LoadInventory();
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Error adjusting inventory quantity");
            await App.Notifications!.ShowErrorAsync("Error", ex.Message);
        }
    }

    // ─── DELETE ───

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int id)
        {
            var item = _allItems.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            bool confirmed = await App.Notifications!.ShowConfirmAsync(
                "Confirm Delete",
                $"Delete \"{item.Name}\"?\nThis action cannot be undone.");

            if (!confirmed) return;

            try
            {
                await App.InventoryService!.DeleteItemAsync(id);
                LoadInventory();
            }
            catch (Exception ex)
            {
                App.Logger?.LogError(ex, "Error deleting inventory item");
                await App.Notifications!.ShowErrorAsync("Error", ex.Message);
            }
        }
    }

    // ─── EXPORT ───

    private async void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_allItems.Count == 0)
            {
                await App.Notifications!.ShowInfoAsync("Export", "No items to export.");
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Inventory",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"inventory_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dlg.ShowDialog() == true)
            {
                var lines = new List<string> { "Name,Category,SubCategory,Quantity,Unit,MinLevel,ReorderQuantity,CostPrice,Description" };
                foreach (var item in _allItems)
                {
                    var name = (item.Name ?? "").Replace("\"", "\"\"");
                    var cat = (item.Category ?? "").Replace("\"", "\"\"");
                    var sub = (item.SubCategory ?? "").Replace("\"", "\"\"");
                    var unit = (item.Unit ?? "").Replace("\"", "\"\"");
                    var desc = (item.Description ?? "").Replace("\"", "\"\"");
                    lines.Add($"\"{name}\",\"{cat}\",\"{sub}\",{item.Quantity},\"{unit}\",{item.MinLevel},{item.ReorderQuantity},{item.CostPrice:F2},\"{desc}\"");
                }
                File.WriteAllLines(dlg.FileName, lines);
                await App.Notifications!.ShowSuccessAsync(
                    "Export Complete",
                    $"Exported {_allItems.Count} items successfully!");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── IMPORT ───

    private async void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import Inventory",
            Filter = "CSV files (*.csv)|*.csv"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var lines = File.ReadAllLines(dlg.FileName);
            if (lines.Length < 2)
            {
                MessageBox.Show("CSV file is empty or has no data rows.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse header to find column indices
            var header = ParseCsvLine(lines[0]);
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
                colMap[header[i].Trim()] = i;

            // Require at least Name column
            if (!colMap.ContainsKey("Name"))
            {
                MessageBox.Show("CSV must have a 'Name' column.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int updated = 0, added = 0, skipped = 0;

            for (int row = 1; row < lines.Length; row++)
            {
                if (string.IsNullOrWhiteSpace(lines[row])) continue;

                var cols = ParseCsvLine(lines[row]);
                var name = GetCol(cols, colMap, "Name")?.Trim();
                if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                var category = GetCol(cols, colMap, "Category")?.Trim();
                var subCategory = GetCol(cols, colMap, "SubCategory")?.Trim();
                var unit = GetCol(cols, colMap, "Unit")?.Trim();
                var desc = GetCol(cols, colMap, "Description")?.Trim();
                int.TryParse(GetCol(cols, colMap, "Quantity"), out var qty);
                int.TryParse(GetCol(cols, colMap, "MinLevel"), out var minLvl);
                int.TryParse(GetCol(cols, colMap, "ReorderQuantity"), out var reorderQty);
                decimal.TryParse(GetCol(cols, colMap, "CostPrice"), out var cost);

                // Check if item with same name exists
                var existing = _allItems.FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // Update existing item
                    existing.Category = string.IsNullOrWhiteSpace(category) ? existing.Category : category;
                    existing.SubCategory = string.IsNullOrWhiteSpace(subCategory) ? existing.SubCategory : subCategory;
                    existing.Unit = string.IsNullOrWhiteSpace(unit) ? existing.Unit : unit;
                    existing.Description = string.IsNullOrWhiteSpace(desc) ? existing.Description : desc;
                    existing.Quantity = qty > 0 ? qty : existing.Quantity;
                    existing.MinLevel = minLvl > 0 ? minLvl : existing.MinLevel;
                    existing.ReorderQuantity = reorderQty > 0 ? reorderQty : existing.ReorderQuantity;
                    existing.CostPrice = cost > 0 ? cost : existing.CostPrice;
                    await App.InventoryService!.UpdateItemAsync(existing);
                    updated++;
                }
                else
                {
                    // Add new item
                    var newItem = new InventoryItem
                    {
                        Name = name,
                        Category = string.IsNullOrWhiteSpace(category) ? null : category,
                        SubCategory = string.IsNullOrWhiteSpace(subCategory) ? null : subCategory,
                        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit,
                        Description = string.IsNullOrWhiteSpace(desc) ? null : desc,
                        Quantity = qty,
                        MinLevel = minLvl,
                        ReorderQuantity = reorderQty,
                        CostPrice = cost
                    };
                    await App.InventoryService!.CreateItemAsync(newItem);
                    added++;
                }
            }

            LoadInventory();
            MessageBox.Show($"Import complete!\n\n• {added} new items added\n• {updated} existing items updated\n• {skipped} rows skipped",
                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? GetCol(string[] cols, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(key, out var idx) && idx < cols.Length ? cols[idx] : null;
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else
                    { inQuotes = false; }
                }
                else { current.Append(c); }
            }
            else
            {
                if (c == '"') { inQuotes = true; }
                else if (c == ',')
                { fields.Add(current.ToString()); current.Clear(); }
                else { current.Append(c); }
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
