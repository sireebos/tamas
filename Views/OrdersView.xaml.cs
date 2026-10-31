
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Printing;
using RestaurantManager.Models;

namespace RestaurantManager.Views;

public partial class OrdersView : UserControl
{
    private List<InventoryItem> _allMenuItems = new();
    private List<SubCategory> _allSubCategories = new();
    private string _selectedCategory = "All";
    private string _selectedSubCategory = "All";
    private HashSet<string>? _allowedCategories = null;
    private BrandingSettings _settings = new();

    // ─── MULTI-TAB STATE ───
    private List<OrderTabData> _orderTabs = new();
    private int _activeTabIndex = -1;

    public event Action? BackRequested;
    public bool OpenToHistory { get; set; } = false;

    public OrdersView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            await LoadSettings();
            LoadMenuItems();
            if (OpenToHistory)
            {
                // We still need an underlying blank tab in case they edit an order later
                CreateNewOrderTab();

                // Go directly to order history
                NewOrderPanel.Visibility = Visibility.Collapsed;
                OrderHistoryPanel.Visibility = Visibility.Visible;
                OrderTabsStrip.Visibility = Visibility.Collapsed;
                TabNewOrder.Visibility = Visibility.Visible;
                TabOrderHistory.Visibility = Visibility.Collapsed;
                LoadOrderHistory();
            }
            else
            {
                CreateNewOrderTab();
            }
        };
    }

    // ─── HELPER CLASSES ───

    private class OrderItemEntry
    {
        public int InventoryItemId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal Total => Price * Quantity;
    }

    private class OrderTabData
    {
        public string Label { get; set; } = "New Order";
        public List<OrderItemEntry> Items { get; set; } = new();
        public string TableNumber { get; set; } = "";
        public string Notes { get; set; } = "";
        public int? EditingOrderId { get; set; }
    }

    // ─── CURRENCY ───

    private async System.Threading.Tasks.Task LoadSettings()
    {
        try
        {
            _settings = await App.BrandingService!.GetSettingsAsync();
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to load branding settings"); }
    }

    private string Fmt(decimal amount) => _settings.FormatCurrency(amount);

    // ─── KEYBOARD SHORTCUTS (called from MainWindow) ───

    public void ShortcutPlaceOrder() => PlaceOrder_Click(this, new RoutedEventArgs());
    public void ShortcutNewTab() => CreateNewOrderTab();
    public void ShortcutSaveDraft() => SaveDraft_Click(this, new RoutedEventArgs());
    public void ShortcutHistory() => TabOrderHistory_Click(this, new RoutedEventArgs());
    public void ShortcutClearOrder() => ClearOrder_Click(this, new RoutedEventArgs());
    public void ShortcutFocusSearch() { MenuSearchBox.Focus(); MenuSearchBox.SelectAll(); }
    public void ShortcutEscape()
    {
        if (PaymentOverlay.Visibility == Visibility.Visible)
            PaymentOverlay.Visibility = Visibility.Collapsed;
        else if (OrderDetailOverlay.Visibility == Visibility.Visible)
            OrderDetailOverlay.Visibility = Visibility.Collapsed;
    }

    // ─── TAB SWITCHING ───

    private async void BackBtn_Click(object sender, RoutedEventArgs e)
    {
        // Check if any tab has items
        SaveCurrentTabState();
        bool hasData = _orderTabs.Any(t => t.Items.Count > 0);
        if (hasData)
        {
            bool confirmed = await App.Notifications!.ShowConfirmAsync(
                "Unsaved Orders",
                "You have open order tabs with items. Going back will discard all unsaved order data.\n\nAre you sure you want to go back?");
            if (!confirmed) return;
        }
        BackRequested?.Invoke();
    }

    private void TabNewOrder_Click(object sender, RoutedEventArgs e)
    {
        CreateNewOrderTab();
        ShowNewOrderPanel();
    }

    private void TabOrderHistory_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentTabState();
        NewOrderPanel.Visibility = Visibility.Collapsed;
        OrderHistoryPanel.Visibility = Visibility.Visible;
        OrderTabsStrip.Visibility = Visibility.Collapsed;
        TabNewOrder.Visibility = Visibility.Visible;
        TabOrderHistory.Visibility = Visibility.Collapsed;
        LoadOrderHistory();
    }

    private void ShowNewOrderPanel()
    {
        NewOrderPanel.Visibility = Visibility.Visible;
        OrderHistoryPanel.Visibility = Visibility.Collapsed;
        OrderTabsStrip.Visibility = Visibility.Visible;
        TabNewOrder.Visibility = Visibility.Visible;
        TabOrderHistory.Visibility = Visibility.Visible;
    }

    // ─── ORDER TABS ───

    private void CreateNewOrderTab()
    {
        SaveCurrentTabState();
        var tabNumber = _orderTabs.Count + 1;
        var tab = new OrderTabData { Label = $"Order #{tabNumber}" };
        _orderTabs.Add(tab);
        _activeTabIndex = _orderTabs.Count - 1;
        ShowNewOrderPanel();
        RenderOrderTabs();
        RestoreTabState();
    }

    private void SwitchToTab(int index)
    {
        if (index < 0 || index >= _orderTabs.Count || index == _activeTabIndex) return;
        SaveCurrentTabState();
        _activeTabIndex = index;
        ShowNewOrderPanel();
        RenderOrderTabs();
        RestoreTabState();
    }

    private async void CloseTab(int index)
    {
        if (index < 0 || index >= _orderTabs.Count) return;

        var tab = _orderTabs[index];
        if (tab.Items.Count > 0)
        {
            bool confirmed = await App.Notifications!.ShowConfirmAsync("Close Tab",
                $"Close \"{tab.Label}\"? Unsaved items will be lost.");
            if (!confirmed) return;
        }

        _orderTabs.RemoveAt(index);

        if (_orderTabs.Count == 0)
        {
            CreateNewOrderTab();
            return;
        }

        if (_activeTabIndex >= _orderTabs.Count)
            _activeTabIndex = _orderTabs.Count - 1;
        else if (index < _activeTabIndex)
            _activeTabIndex--;
        else if (index == _activeTabIndex)
            _activeTabIndex = Math.Min(index, _orderTabs.Count - 1);

        RenderOrderTabs();
        RestoreTabState();
    }

    private void SaveCurrentTabState()
    {
        if (_activeTabIndex < 0 || _activeTabIndex >= _orderTabs.Count) return;
        var tab = _orderTabs[_activeTabIndex];
        tab.TableNumber = OrderTableNumber.Text;
        tab.Notes = OrderNotes.Text;
    }

    private void RestoreTabState()
    {
        if (_activeTabIndex < 0 || _activeTabIndex >= _orderTabs.Count) return;
        var tab = _orderTabs[_activeTabIndex];
        OrderTableNumber.Text = tab.TableNumber;
        OrderNotes.Text = tab.Notes;
        OrderHeaderTitle.Text = tab.EditingOrderId.HasValue ? $"Edit {tab.Label}" : tab.Label;
        OrderHeaderSub.Text = tab.EditingOrderId.HasValue ? "Editing existing order" : "Add items from the menu";
        RenderOrderItems();
    }

    private List<OrderItemEntry> CurrentItems =>
        _activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count
            ? _orderTabs[_activeTabIndex].Items
            : new();

    private int? CurrentEditingOrderId
    {
        get => _activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count ? _orderTabs[_activeTabIndex].EditingOrderId : null;
        set { if (_activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count) _orderTabs[_activeTabIndex].EditingOrderId = value; }
    }

    private void RenderOrderTabs()
    {
        OrderTabsStrip.Children.Clear();

        for (int i = 0; i < _orderTabs.Count; i++)
        {
            var tab = _orderTabs[i];
            var isActive = i == _activeTabIndex;
            var idx = i;

            var tabGrid = new Grid();
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var itemCount = tab.Items.Sum(x => x.Quantity);
            var label = itemCount > 0 ? $"{tab.Label} ({itemCount})" : tab.Label;

            var tabBtn = new Button
            {
                Content = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
                Height = 34, Padding = new Thickness(14, 0, 8, 0),
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea))
                    : new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                Foreground = isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            tabBtn.Click += (s, _) => SwitchToTab(idx);
            Grid.SetColumn(tabBtn, 0);
            tabGrid.Children.Add(tabBtn);

            var closeBtn = new Button
            {
                Width = 24, Height = 24,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0),
                ToolTip = "Close tab"
            };
            closeBtn.Template = CreateCloseBtnTemplate(isActive);
            closeBtn.Click += (s, _) => CloseTab(idx);
            Grid.SetColumn(closeBtn, 1);
            tabGrid.Children.Add(closeBtn);

            var tabBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea))
                    : new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                Padding = new Thickness(0, 0, 6, 0),
                Margin = new Thickness(0, 0, 6, 0),
                Child = tabGrid
            };
            OrderTabsStrip.Children.Add(tabBorder);
        }
    }

    private ControlTemplate CreateCloseBtnTemplate(bool isActive)
    {
        var template = new ControlTemplate(typeof(Button));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.WidthProperty, 22.0);
        factory.SetValue(Border.HeightProperty, 22.0);
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        factory.SetValue(Border.BackgroundProperty, isActive
            ? new SolidColorBrush(Color.FromArgb(0x44, 0xff, 0xff, 0xff))
            : new SolidColorBrush(Color.FromRgb(0xcd, 0xd5, 0xdf)));
        var txt = new FrameworkElementFactory(typeof(TextBlock));
        txt.SetValue(TextBlock.TextProperty, "✕");
        txt.SetValue(TextBlock.FontSizeProperty, 12.0);
        txt.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        txt.SetValue(TextBlock.ForegroundProperty, isActive ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
        txt.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        txt.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.AppendChild(txt);
        template.VisualTree = factory;
        return template;
    }

    // ─── MENU ITEMS ───

    private async void LoadMenuItems()
    {
        try
        {
            _allMenuItems = await App.InventoryService!.GetAllItemsAsync();
            _allSubCategories = await App.SubCategoryService!.GetAllAsync();

            // Filter by user's department assignments
            if (App.CurrentUser != null)
            {
                var userDepts = await App.DepartmentService!.GetUserDepartmentsForUserAsync(App.CurrentUser.Id);
                if (userDepts.Count > 0)
                {
                    // User is assigned to department(s) — only show categories linked to those departments
                    var allowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var dept in userDepts)
                    {
                        var catDepts = await App.CategoryDepartmentService!.GetCategoriesForDepartmentAsync(dept.Id);
                        foreach (var cd in catDepts)
                        {
                            if (cd.Category != null)
                                allowedCategories.Add(cd.Category.Name);
                        }
                    }

                    if (allowedCategories.Count > 0)
                    {
                        _allowedCategories = allowedCategories;
                    }
                }
                // If no departments assigned, show all items (default)
            }

            BuildCategoryTabs();
            RenderMenuItems();
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Error loading menu");
            await App.Notifications!.ShowErrorAsync("Error", $"Error loading menu: {ex.Message}");
        }
    }

    private void BuildCategoryTabs()
    {
        MenuCategoryTabs.Children.Clear();

        // Get category tab size setting
        var catTabSize = _settings?.CategoryTabSize ?? "medium";
        double tabHeight = catTabSize switch { "small" => 36, "large" => 52, "xlarge" => 62, _ => 42 };
        double tabFontSize = catTabSize switch { "small" => 11, "large" => 16, "xlarge" => 19, _ => 13 };
        var tabPadding = catTabSize switch { "small" => new Thickness(12, 4, 12, 4), "large" => new Thickness(24, 8, 24, 8), "xlarge" => new Thickness(30, 10, 30, 10), _ => new Thickness(18, 6, 18, 6) };
        double tabCorner = catTabSize switch { "small" => 8, "large" => 14, "xlarge" => 16, _ => 10 };

        // Only show 'All' button if the user has more than one category to choose from
        bool showAll = _allowedCategories == null || _allowedCategories.Count > 1;
        if (showAll)
        {
            var allBtn = new Button { Content = "All", Style = FindResource("CatTab") as Style, Height = tabHeight, FontSize = tabFontSize, Padding = tabPadding };
            SetCatTabActive(allBtn, _selectedCategory == "All");
            allBtn.Click += (s, _) => { _selectedCategory = "All"; _selectedSubCategory = "All"; HighlightCategoryTab(); BuildSubCategoryTabs(); RenderMenuItems(); };
            MenuCategoryTabs.Children.Add(allBtn);
        }
        else if (_selectedCategory == "All" && _allowedCategories?.Count > 0)
        {
            // Auto-select the only available category if "All" is no longer an option
            _selectedCategory = _allowedCategories.First();
        }

        var categories = _allMenuItems.Where(m => !string.IsNullOrEmpty(m.Category)).Select(m => m.Category!).Distinct().OrderBy(c => c);
        
        // Apply category authorization filter for the tab display
        if (_allowedCategories != null)
        {
            categories = categories.Where(c => _allowedCategories.Contains(c)).OrderBy(c => c);
        }

        foreach (var cat in categories)
        {
            var btn = new Button { Content = cat, Style = FindResource("CatTab") as Style, Height = tabHeight, FontSize = tabFontSize, Padding = tabPadding };
            var catCopy = cat;
            SetCatTabActive(btn, _selectedCategory == catCopy);
            btn.Click += (s, _) => 
            { 
                _selectedCategory = catCopy; 
                _selectedSubCategory = "All"; 
                HighlightCategoryTab(); 
                BuildSubCategoryTabs();
                RenderMenuItems(); 
            };
            MenuCategoryTabs.Children.Add(btn);
        }

        BuildSubCategoryTabs();
    }

    private void BuildSubCategoryTabs()
    {
        MenuSubCategoryTabs.Children.Clear();
        
        if (_selectedCategory == "All") 
        {
            SubCategoryScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        var subCategories = _allMenuItems
            .Where(m => m.Category == _selectedCategory && !string.IsNullOrEmpty(m.SubCategory))
            .Select(m => m.SubCategory!)
            .ToList();

        // Add from formal sub-categories table
        var formalSubs = _allSubCategories
            .Where(sc => sc.Category?.Name == _selectedCategory)
            .Select(sc => sc.Name);
        
        foreach (var sub in formalSubs)
        {
            if (!subCategories.Contains(sub, StringComparer.OrdinalIgnoreCase))
                subCategories.Add(sub);
        }

        subCategories = subCategories.Distinct().OrderBy(c => c).ToList();

        if (subCategories.Count == 0)
        {
            SubCategoryScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        SubCategoryScrollViewer.Visibility = Visibility.Visible;

        // Get category tab size setting (sub tabs are slightly smaller)
        var catTabSize = _settings?.CategoryTabSize ?? "medium";
        double subHeight = catTabSize switch { "small" => 30, "large" => 42, "xlarge" => 50, _ => 34 };
        double subFontSize = catTabSize switch { "small" => 10, "large" => 14, "xlarge" => 16, _ => 11 };

        var allBtn = new Button { Content = "All Types", Style = FindResource("CatTab") as Style, Height = subHeight, FontSize = subFontSize, Margin = new Thickness(0,0,6,0) };
        SetCatTabActive(allBtn, _selectedSubCategory == "All");
        allBtn.Click += (s, _) => { _selectedSubCategory = "All"; HighlightSubCategoryTab(); RenderMenuItems(); };
        MenuSubCategoryTabs.Children.Add(allBtn);

        foreach (var sub in subCategories)
        {
            var btn = new Button { Content = sub, Style = FindResource("CatTab") as Style, Height = subHeight, FontSize = subFontSize, Margin = new Thickness(0,0,6,0) };
            SetCatTabActive(btn, _selectedSubCategory == sub);
            var subCopy = sub;
            btn.Click += (s, _) => { _selectedSubCategory = subCopy; HighlightSubCategoryTab(); RenderMenuItems(); };
            MenuSubCategoryTabs.Children.Add(btn);
        }
    }

    private void HighlightSubCategoryTab()
    {
        foreach (Button btn in MenuSubCategoryTabs.Children)
        {
            var content = btn.Content?.ToString() ?? "";
            SetCatTabActive(btn, content == _selectedSubCategory || (content == "All Types" && _selectedSubCategory == "All"));
        }
    }

    private void HighlightCategoryTab()
    {
        foreach (Button btn in MenuCategoryTabs.Children)
        {
            var content = btn.Content?.ToString() ?? "";
            SetCatTabActive(btn, content == _selectedCategory);
        }
    }

    private void SetCatTabActive(Button btn, bool active)
    {
        if (active)
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));
        }
    }

    private void RenderMenuItems()
    {
        MenuItemsPanel.Children.Clear();
        var searchText = MenuSearchBox.Text.Trim().ToLower();

        var items = _allMenuItems.AsEnumerable();
        
        if (_selectedSubCategory != "All")
        {
            // If a specific sub-category is chosen, show items matching that sub-category name
            // regardless of their parent category (to support cross-assigned sub-categories)
            items = items.Where(m => m.SubCategory == _selectedSubCategory);
        }
        else if (_selectedCategory != "All")
        {
            // If no sub-category is chosen, filter by main category
            items = items.Where(m => m.Category == _selectedCategory);
        }
        else if (_allowedCategories != null)
        {
            // Category "All" is selected, but the user is restricted — show only authorized items
            items = items.Where(m => !string.IsNullOrEmpty(m.Category) && _allowedCategories.Contains(m.Category));
        }
        
        if (!string.IsNullOrEmpty(searchText))
            items = items.Where(m => m.Name.ToLower().Contains(searchText) || (m.Description?.ToLower().Contains(searchText) ?? false));

        foreach (var item in items)
        {
            var card = CreateMenuItemCard(item);
            MenuItemsPanel.Children.Add(card);
        }
    }

    private Border CreateMenuItemCard(InventoryItem item)
    {
        // Get card size setting
        var cardSize = _settings?.ProductCardSize ?? "medium";
        double cardWidth = cardSize switch { "small" => 150, "large" => 240, "xlarge" => 280, "xxlarge" => 340, "xxxlarge" => 400, _ => 180 };
        double titleFont = cardSize switch { "small" => 12, "large" => 15, "xlarge" => 17, "xxlarge" => 20, "xxxlarge" => 24, _ => 13 };
        double descFont = cardSize switch { "small" => 9, "large" => 11, "xlarge" => 12, "xxlarge" => 14, "xxxlarge" => 16, _ => 10 };
        double priceFont = cardSize switch { "small" => 12, "large" => 16, "xlarge" => 18, "xxlarge" => 22, "xxxlarge" => 26, _ => 14 };
        double catFont = cardSize switch { "small" => 8, "large" => 10, "xlarge" => 11, "xxlarge" => 13, "xxxlarge" => 15, _ => 9 };
        double padH = cardSize switch { "small" => 12, "large" => 20, "xlarge" => 24, "xxlarge" => 28, "xxxlarge" => 32, _ => 16 };
        double padV = cardSize switch { "small" => 10, "large" => 18, "xlarge" => 22, "xxlarge" => 26, "xxxlarge" => 30, _ => 14 };
        double cornerR = cardSize switch { "small" => 10, "large" => 18, "xlarge" => 20, "xxlarge" => 22, "xxxlarge" => 24, _ => 14 };

        // Custom font overrides
        int customFontSize = _settings?.ProductCardFontSize ?? 0;
        if (customFontSize > 0)
        {
            titleFont = customFontSize;
            descFont = Math.Max(customFontSize - 3, 8);
            priceFont = customFontSize + 1;
            catFont = Math.Max(customFontSize - 4, 7);
        }

        var fontWeight = (_settings?.ProductCardFontWeight ?? "SemiBold") switch
        {
            "Normal" => FontWeights.Normal,
            "Bold" => FontWeights.Bold,
            "ExtraBold" => FontWeights.ExtraBold,
            _ => FontWeights.SemiBold
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = item.Name, FontSize = titleFont, FontWeight = fontWeight,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (!string.IsNullOrEmpty(item.Description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = item.Description, FontSize = descFont,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = (cardSize == "large" || cardSize == "xlarge" || cardSize == "xxlarge" || cardSize == "xxxlarge") ? 36 : 16,
                TextWrapping = (cardSize == "large" || cardSize == "xlarge" || cardSize == "xxlarge" || cardSize == "xxxlarge") ? TextWrapping.Wrap : TextWrapping.NoWrap
            });
        }

        var bottomRow = new Grid { Margin = new Thickness(0, (cardSize == "small" ? 8 : 12), 0, 0) };
        bottomRow.Children.Add(new TextBlock
        {
            Text = Fmt(item.CostPrice), FontSize = priceFont, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (!string.IsNullOrEmpty(item.Category))
        {
            var catBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff)),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = item.Category, FontSize = catFont, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea))
                }
            };
            bottomRow.Children.Add(catBadge);
        }
        stack.Children.Add(bottomRow);

        var card = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(cornerR),
            Padding = new Thickness(padH, padV, padH, padV), Margin = new Thickness(0, 0, 10, 10),
            Width = cardWidth, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1.5), Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack, Tag = item
        };
        card.PreviewMouseLeftButtonDown += (s, _) => AddMenuItemToOrder(item);
        card.MouseEnter += (s, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        card.MouseLeave += (s, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));

        return card;
    }

    private void MenuSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) RenderMenuItems();
    }

    // ─── ORDER ITEMS MANAGEMENT ───

    private void AddMenuItemToOrder(InventoryItem item)
    {
        var items = CurrentItems;
        var existing = items.FirstOrDefault(o => o.InventoryItemId == item.Id);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            items.Add(new OrderItemEntry
            {
                InventoryItemId = item.Id,
                Name = item.Name,
                Price = item.CostPrice,
                Quantity = 1
            });
        }
        RenderOrderItems();
        RenderOrderTabs();
    }

    private void RenderOrderItems()
    {
        var items = CurrentItems;
        OrderItemsPanel.Children.Clear();
        OrderEmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var entry in items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            // Item info
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            infoStack.Children.Add(new TextBlock
            {
                Text = entry.Name, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"{Fmt(entry.Price)} each", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8))
            });
            Grid.SetColumn(infoStack, 0);
            row.Children.Add(infoStack);

            // Quantity controls
            var qtyPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            var minusBtn = new Button
            {
                Content = "−", Width = 28, Height = 28, FontSize = 14, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = entry
            };
            minusBtn.Click += DecreaseQty_Click;
            qtyPanel.Children.Add(minusBtn);

            qtyPanel.Children.Add(new TextBlock
            {
                Text = entry.Quantity.ToString(), FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
                Width = 30, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            });

            var plusBtn = new Button
            {
                Content = "＋", Width = 28, Height = 28, FontSize = 12, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(0xef, 0xf6, 0xff)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = entry
            };
            plusBtn.Click += IncreaseQty_Click;
            qtyPanel.Children.Add(plusBtn);
            Grid.SetColumn(qtyPanel, 1);
            row.Children.Add(qtyPanel);

            // Line total
            var totalText = new TextBlock
            {
                Text = Fmt(entry.Total), FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
                VerticalAlignment = VerticalAlignment.Center, Width = 70, TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(totalText, 2);
            row.Children.Add(totalText);

            // Remove button
            var removeBtn = new Button
            {
                Content = "✕", FontSize = 13, FontWeight = FontWeights.Bold,
                Width = 26, Height = 26, Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                ToolTip = "Remove item", Tag = entry
            };
            removeBtn.Click += RemoveItem_Click;
            Grid.SetColumn(removeBtn, 3);
            row.Children.Add(removeBtn);

            var itemBorder = new Border
            {
                Background = Brushes.White, Padding = new Thickness(20, 12, 20, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)),
                BorderThickness = new Thickness(0, 0, 0, 1), Child = row
            };
            OrderItemsPanel.Children.Add(itemBorder);
        }

        UpdateTotals();
    }

    private void IncreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OrderItemEntry entry)
        {
            entry.Quantity++;
            RenderOrderItems();
            RenderOrderTabs();
        }
    }

    private void DecreaseQty_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OrderItemEntry entry)
        {
            entry.Quantity--;
            if (entry.Quantity <= 0)
                CurrentItems.Remove(entry);
            RenderOrderItems();
            RenderOrderTabs();
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is OrderItemEntry entry)
        {
            CurrentItems.Remove(entry);
            RenderOrderItems();
            RenderOrderTabs();
        }
    }

    private void UpdateTotals()
    {
        var items = CurrentItems;
        var subtotal = items.Sum(i => i.Total);
        var count = items.Sum(i => i.Quantity);
        SubtotalText.Text = Fmt(subtotal);
        ItemCountText.Text = count.ToString();
        TotalText.Text = Fmt(subtotal);
    }

    private async void ClearOrder_Click(object sender, RoutedEventArgs e)
    {
        var items = CurrentItems;
        if (items.Count == 0) return;
        bool confirmed = await App.Notifications!.ShowConfirmAsync("Clear Order",
            "Clear all items from this order?");
        if (!confirmed) return;

        items.Clear();
        CurrentEditingOrderId = null;
        OrderTableNumber.Text = "";
        OrderNotes.Text = "";
        if (_activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count)
        {
            var tab = _orderTabs[_activeTabIndex];
            OrderHeaderTitle.Text = tab.Label;
        }
        else
        {
            OrderHeaderTitle.Text = "New Order";
        }
        OrderHeaderSub.Text = "Add items from the menu";
        RenderOrderItems();
        RenderOrderTabs();
    }

    // ─── PLACE / SAVE ORDER ───

    private string _selectedPaymentMethod = "";
    private bool _paymentFromHistory = false;
    private decimal _paymentTotal = 0;

    private void PlaceOrder_Click(object sender, RoutedEventArgs e)
    {
        var items = CurrentItems;
        if (items.Count == 0)
        {
            MessageBox.Show("Please add at least one item to the order.", "Empty Order", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show payment overlay
        var total = items.Sum(i => i.Total);
        _paymentTotal = total;
        _paymentFromHistory = false;
        PaymentTotalText.Text = Fmt(total);
        _selectedPaymentMethod = "";
        ConfirmPaymentBtn.IsEnabled = false;
        CashAmountPanel.Visibility = Visibility.Collapsed;
        SplitAmountPanel.Visibility = Visibility.Collapsed;
        ChangePanel.Visibility = Visibility.Collapsed;
        AmountReceivedBox.Text = "";
        ResetPaymentMethodHighlights();
        ApplyPaymentMethodVisibility();
        PaymentOverlay.Visibility = Visibility.Visible;
    }

    private void ApplyPaymentMethodVisibility()
    {
        var enabled = (_settings.EnabledPaymentMethods ?? "cash,transfer,pos,cash_transfer,cash_pos")
            .Split(',', StringSplitOptions.TrimEntries);
        PayCash.Visibility = enabled.Contains("cash") ? Visibility.Visible : Visibility.Collapsed;
        PayTransfer.Visibility = enabled.Contains("transfer") ? Visibility.Visible : Visibility.Collapsed;
        PayPOS.Visibility = enabled.Contains("pos") ? Visibility.Visible : Visibility.Collapsed;
        PayCashTransfer.Visibility = enabled.Contains("cash_transfer") ? Visibility.Visible : Visibility.Collapsed;
        PayCashPOS.Visibility = enabled.Contains("cash_pos") ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        var items = CurrentItems;
        if (items.Count == 0)
        {
            MessageBox.Show("Please add at least one item.", "Empty Order", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            int? tableId = null;
            if (int.TryParse(OrderTableNumber.Text.Trim(), out var tid)) tableId = tid;

            var orderNumber = CurrentEditingOrderId.HasValue ? "" : GenerateOrderNumber();

            if (CurrentEditingOrderId.HasValue)
            {
                var order = await App.OrderService!.GetOrderAsync(CurrentEditingOrderId.Value);
                if (order == null) return;
                order.TableId = tableId;
                order.Notes = OrderNotes.Text.Trim();
                order.Total = items.Sum(i => i.Total);
                order.Items.Clear();
                foreach (var entry in items)
                {
                    order.Items.Add(new OrderItem { InventoryItemId = entry.InventoryItemId, Quantity = entry.Quantity, Price = entry.Price });
                }
                await App.OrderService.UpdateOrderAsync(order);
                MessageBox.Show("Draft updated!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var order = new Order
                {
                    OrderNumber = orderNumber,
                    TableId = tableId,
                    UserId = App.CurrentUser?.Id,
                    Status = "pending",
                    Notes = OrderNotes.Text.Trim(),
                    Total = items.Sum(i => i.Total)
                };
                foreach (var entry in items)
                {
                    order.Items.Add(new OrderItem { InventoryItemId = entry.InventoryItemId, Quantity = entry.Quantity, Price = entry.Price });
                }
                await App.OrderService!.CreateOrderAsync(order);
                MessageBox.Show($"Draft #{orderNumber} saved!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ResetCurrentTab();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            MessageBox.Show($"Error: {msg}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetCurrentTab()
    {
        if (_activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count)
        {
            var tab = _orderTabs[_activeTabIndex];
            tab.Items.Clear();
            tab.EditingOrderId = null;
            tab.TableNumber = "";
            tab.Notes = "";
        }
        OrderTableNumber.Text = "";
        OrderNotes.Text = "";
        if (_activeTabIndex >= 0 && _activeTabIndex < _orderTabs.Count)
            OrderHeaderTitle.Text = _orderTabs[_activeTabIndex].Label;
        else
            OrderHeaderTitle.Text = "New Order";
        OrderHeaderSub.Text = "Add items from the menu";
        RenderOrderItems();
        RenderOrderTabs();
    }

    private string GenerateOrderNumber()
    {
        return $"ORD-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
    }

    // ─── ORDER HISTORY ───

    private List<Order> _allOrders = new();
    private HashSet<int> _selectedOrderIds = new();
    private string _dateFilter = "today"; // "all", "today", "yesterday", "range"
    private bool _isAdmin = false;

    private async void LoadOrderHistory()
    {
        try
        {
            var allOrders = await App.OrderService!.GetAllOrdersAsync();

            // Non-admin users only see their own orders
            var roles = (App.CurrentUser?.Role ?? "").ToLower();
            _isAdmin = roles.Contains("admin") || roles.Contains("developer");

            _allOrders = _isAdmin
                ? allOrders
                : allOrders.Where(o => o.UserId == App.CurrentUser?.Id).ToList();

            _selectedOrderIds.Clear();
            SelectAllOrders.IsChecked = false;
            // Hide select-all and delete for non-admins
            SelectAllOrders.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;
            UpdateDeleteSelectedButton();
            SetDateFilterActive(_dateFilter);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}");
        }
    }

    private void RenderOrderHistory()
    {
        OrderHistoryList.Children.Clear();

        var searchText = OrderSearchBox.Text.Trim().ToLower();
        var statusFilter = (HistoryStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Orders";

        var orders = _allOrders.AsEnumerable();

        if (statusFilter != "All Orders")
            orders = orders.Where(o => o.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(searchText))
            orders = orders.Where(o => o.OrderNumber.ToLower().Contains(searchText)
                || (o.Notes?.ToLower().Contains(searchText) ?? false)
                || o.TableId?.ToString().Contains(searchText) == true);

        // Date filtering
        var today = DateTime.Today;
        switch (_dateFilter)
        {
            case "today":
                orders = orders.Where(o => o.CreatedAt.Date == today);
                break;
            case "yesterday":
                orders = orders.Where(o => o.CreatedAt.Date == today.AddDays(-1));
                break;
            case "range":
                if (DateFrom.SelectedDate.HasValue)
                    orders = orders.Where(o => o.CreatedAt.Date >= DateFrom.SelectedDate.Value.Date);
                if (DateTo.SelectedDate.HasValue)
                    orders = orders.Where(o => o.CreatedAt.Date <= DateTo.SelectedDate.Value.Date);
                break;
        }

        var filtered = orders.ToList();
        OrderCountText.Text = $"{filtered.Count} order(s)";

        foreach (var order in filtered)
        {
            var card = CreateOrderHistoryCard(order);
            OrderHistoryList.Children.Add(card);
        }

        if (filtered.Count == 0)
        {
            OrderHistoryList.Children.Add(new TextBlock
            {
                Text = "No orders found", FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 0)
            });
        }
    }

    private Border CreateOrderHistoryCard(Order order)
    {
        var headerGrid = new Grid();
        int col = 0;

        // Column: Checkbox (admin only)
        if (_isAdmin)
        {
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            var cb = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Tag = order.Id,
                IsChecked = _selectedOrderIds.Contains(order.Id)
            };
            cb.Checked += OrderCheckbox_Changed;
            cb.Unchecked += OrderCheckbox_Changed;
            Grid.SetColumn(cb, col);
            headerGrid.Children.Add(cb);
            col++;
        }

        // Column: Status icon
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Status icon
        var (statusColor, statusBg, statusIcon) = GetStatusStyle(order.Status);

        var iconBorder = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(statusBg),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock { Text = statusIcon, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(iconBorder, col);
        headerGrid.Children.Add(iconBorder);
        col++;

        // Column: Order info (star width)
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = $"#{order.OrderNumber}", FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)), Margin = new Thickness(0, 0, 10, 0)
        });

        var statusBadge = new Border
        {
            Background = new SolidColorBrush(statusBg), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = order.Status.ToUpper(), FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(statusColor)
            }
        };
        titleRow.Children.Add(statusBadge);
        infoStack.Children.Add(titleRow);

        var userName = order.User?.Name ?? "Unknown";
        var detailText = $"By: {userName}  •  Table: {(order.TableId?.ToString() ?? "—")}  •  {order.Items.Count} item(s)  •  {order.CreatedAt:MMM dd, HH:mm}";
        infoStack.Children.Add(new TextBlock { Text = detailText, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)), Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(infoStack, col);
        headerGrid.Children.Add(infoStack);
        col++;

        // Column: Total
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var totalStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 14, 0) };
        totalStack.Children.Add(new TextBlock { Text = "TOTAL", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)), FontWeight = FontWeights.Bold });
        totalStack.Children.Add(new TextBlock
        {
            Text = Fmt(order.Total), FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e))
        });
        Grid.SetColumn(totalStack, col);
        headerGrid.Children.Add(totalStack);
        col++;

        // Column: Action buttons
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var viewBtn = new Button
        {
            Content = order.Status == "completed" ? "👁" : "✏", FontSize = 14, Width = 38, Height = 38, Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = order, ToolTip = order.Status == "completed" ? "View / Print" : "View / Edit", Margin = new Thickness(0, 0, 6, 0)
        };
        viewBtn.Click += ViewOrder_Click;
        actionsPanel.Children.Add(viewBtn);

        // Delete button (admin only)
        if (_isAdmin)
        {
            var delBtn = new Button
            {
                Content = "🗑", FontSize = 14, Width = 38, Height = 38, Padding = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = order.Id, ToolTip = "Delete order"
            };
            delBtn.Click += DeleteOrder_Click;
            actionsPanel.Children.Add(delBtn);
        }

        Grid.SetColumn(actionsPanel, col);
        headerGrid.Children.Add(actionsPanel);

        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Child = headerGrid
        };
    }

    private (Color fg, Color bg, string icon) GetStatusStyle(string status) => status.ToLower() switch
    {
        "pending" => (Color.FromRgb(0xf5, 0x9e, 0x0b), Color.FromRgb(0xff, 0xf7, 0xed), "⏳"),
        "preparing" => (Color.FromRgb(0x38, 0xbd, 0xf8), Color.FromRgb(0xf0, 0xf9, 0xff), "👨‍🍳"),
        "ready" => (Color.FromRgb(0x22, 0xc5, 0x5e), Color.FromRgb(0xf0, 0xfd, 0xf4), "✅"),
        "completed" => (Color.FromRgb(0x64, 0x74, 0x8b), Color.FromRgb(0xf1, 0xf5, 0xf9), "✓"),
        "cancelled" => (Color.FromRgb(0xef, 0x44, 0x44), Color.FromRgb(0xfe, 0xf2, 0xf2), "❌"),
        _ => (Color.FromRgb(0x64, 0x74, 0x8b), Color.FromRgb(0xf1, 0xf5, 0xf9), "📋")
    };

    private void OrderSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) RenderOrderHistory();
    }

    private void HistoryStatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RenderOrderHistory();
    }

    // ─── DATE FILTERS ───

    private void SetDateFilterActive(string filter)
    {
        _dateFilter = filter;
        var activeColor = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var inactiveColor = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9));
        var activeFg = Brushes.White;
        var inactiveFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));

        DateFilterAll.Background = filter == "all" ? activeColor : inactiveColor;
        DateFilterAll.Foreground = filter == "all" ? activeFg : inactiveFg;
        DateFilterToday.Background = filter == "today" ? activeColor : inactiveColor;
        DateFilterToday.Foreground = filter == "today" ? activeFg : inactiveFg;
        DateFilterYesterday.Background = filter == "yesterday" ? activeColor : inactiveColor;
        DateFilterYesterday.Foreground = filter == "yesterday" ? activeFg : inactiveFg;
        DateFilterRange.Background = filter == "range" ? activeColor : inactiveColor;
        DateFilterRange.Foreground = filter == "range" ? activeFg : inactiveFg;

        DateRangePickers.Visibility = filter == "range" ? Visibility.Visible : Visibility.Collapsed;

        if (IsLoaded) RenderOrderHistory();
    }

    private void DateFilterAll_Click(object sender, RoutedEventArgs e) => SetDateFilterActive("all");
    private void DateFilterToday_Click(object sender, RoutedEventArgs e) => SetDateFilterActive("today");
    private void DateFilterYesterday_Click(object sender, RoutedEventArgs e) => SetDateFilterActive("yesterday");
    private void DateFilterRange_Click(object sender, RoutedEventArgs e) => SetDateFilterActive("range");

    private void DateRange_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _dateFilter == "range") RenderOrderHistory();
    }

    // ─── VIEW / EDIT ORDER DETAIL ───

    private Order? _detailOrder;

    private void ViewOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Order order) return;
        ShowOrderDetail(order);
    }

    private void ShowOrderDetail(Order order)
    {
        _detailOrder = order;
        DetailTitle.Text = $"Order #{order.OrderNumber}";
        DetailSubtitle.Text = $"Created: {order.CreatedAt:MMM dd, yyyy HH:mm}";
        DetailTable.Text = order.TableId?.ToString() ?? "";
        DetailNotes.Text = order.Notes ?? "";
        DetailTotal.Text = Fmt(order.Total);
        DetailStatus.Text = "";

        // Set status combo
        foreach (ComboBoxItem item in DetailStatusCombo.Items)
        {
            if (item.Content?.ToString() == order.Status)
            {
                DetailStatusCombo.SelectedItem = item;
                break;
            }
        }

        // Render items
        DetailItemsList.Children.Clear();
        foreach (var item in order.Items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };

            var nameText = new TextBlock
            {
                Text = $"{item.InventoryItem?.Name ?? $"Item #{item.InventoryItemId}"} × {item.Quantity}",
                FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
            };
            row.Children.Add(nameText);

            var priceText = new TextBlock
            {
                Text = Fmt(item.Price * item.Quantity), FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.Children.Add(priceText);

            var itemBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8),
                Child = row
            };
            DetailItemsList.Children.Add(itemBorder);
        }

        // Configure buttons and fields based on status
        var isPending = order.Status == "pending";
        var isCompleted = order.Status == "completed";
        DetailPayBtn.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;
        DetailPrintBtn.Visibility = isCompleted ? Visibility.Visible : Visibility.Collapsed;
        DetailSaveBtn.Visibility = isCompleted ? Visibility.Collapsed : Visibility.Visible;
        DetailTable.IsReadOnly = isCompleted;
        DetailNotes.IsReadOnly = isCompleted;
        DetailStatusCombo.IsEnabled = !isCompleted;
        DetailTable.Opacity = isCompleted ? 0.6 : 1;
        DetailNotes.Opacity = isCompleted ? 0.6 : 1;
        DetailStatusCombo.Opacity = isCompleted ? 0.6 : 1;

        OrderDetailOverlay.Visibility = Visibility.Visible;
    }

    private void CloseOrderDetail_Click(object sender, RoutedEventArgs e)
    {
        OrderDetailOverlay.Visibility = Visibility.Collapsed;
        _detailOrder = null;
    }

    private void PrintFromHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_detailOrder == null) return;

        var items = _detailOrder.Items?.Select(oi => new OrderItemEntry
        {
            InventoryItemId = oi.InventoryItemId ?? 0,
            Name = oi.InventoryItem?.Name ?? "Item",
            Price = oi.Price,
            Quantity = oi.Quantity
        }).ToList() ?? new List<OrderItemEntry>();

        var paymentMethod = _detailOrder.PaymentMethod ?? "cash";
        PrintReceipt(_detailOrder, items, paymentMethod);
    }

    private void PayNow_Click(object sender, RoutedEventArgs e)
    {
        if (_detailOrder == null) return;

        // Save any pending changes first
        int? tableId = null;
        if (int.TryParse(DetailTable.Text.Trim(), out var tid)) tableId = tid;
        _detailOrder.TableId = tableId;
        _detailOrder.Notes = DetailNotes.Text.Trim();

        _paymentTotal = _detailOrder.Total;
        _paymentFromHistory = true;
        _selectedPaymentMethod = "";
        ConfirmPaymentBtn.IsEnabled = false;
        CashAmountPanel.Visibility = Visibility.Collapsed;
        SplitAmountPanel.Visibility = Visibility.Collapsed;
        ChangePanel.Visibility = Visibility.Collapsed;
        AmountReceivedBox.Text = "";
        ResetPaymentMethodHighlights();
        PaymentTotalText.Text = Fmt(_detailOrder.Total);
        OrderDetailOverlay.Visibility = Visibility.Collapsed;
        ApplyPaymentMethodVisibility();
        PaymentOverlay.Visibility = Visibility.Visible;
    }

    private async void UpdateOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_detailOrder == null) return;

        try
        {
            var newStatus = (DetailStatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? _detailOrder.Status;
            int? tableId = null;
            if (int.TryParse(DetailTable.Text.Trim(), out var tid)) tableId = tid;

            _detailOrder.TableId = tableId;
            _detailOrder.Notes = DetailNotes.Text.Trim();

            // If status is being set to completed, show payment overlay
            if (newStatus == "completed" && _detailOrder.Status != "completed")
            {
                _detailOrder.Status = newStatus;
                _paymentTotal = _detailOrder.Total;
                _paymentFromHistory = true;
                _selectedPaymentMethod = "";
                ConfirmPaymentBtn.IsEnabled = false;
                CashAmountPanel.Visibility = Visibility.Collapsed;
                SplitAmountPanel.Visibility = Visibility.Collapsed;
                ChangePanel.Visibility = Visibility.Collapsed;
                AmountReceivedBox.Text = "";
                ResetPaymentMethodHighlights();
                PaymentTotalText.Text = Fmt(_detailOrder.Total);
                OrderDetailOverlay.Visibility = Visibility.Collapsed;
                ApplyPaymentMethodVisibility();
                PaymentOverlay.Visibility = Visibility.Visible;
                return;
            }

            _detailOrder.Status = newStatus;
            await App.OrderService!.UpdateOrderAsync(_detailOrder);

            // Hide the overlay and load order back into new order area
            OrderDetailOverlay.Visibility = Visibility.Collapsed;

            CurrentEditingOrderId = _detailOrder.Id;
            OrderTableNumber.Text = _detailOrder.TableId?.ToString() ?? "";
            OrderNotes.Text = _detailOrder.Notes ?? "";
            
            CurrentItems.Clear();
            foreach (var item in _detailOrder.Items)
            {
                CurrentItems.Add(new OrderItemEntry
                {
                    InventoryItemId = item.InventoryItemId ?? 0,
                    Name = item.InventoryItem?.Name ?? "Item",
                    Price = item.Price,
                    Quantity = item.Quantity
                });
            }

            OrderHeaderTitle.Text = $"Edit Order #{_detailOrder.OrderNumber}";
            OrderHeaderSub.Text = "Modify items and save";
            
            RenderOrderItems();

            // Make the new order panel the active screen
            ShowNewOrderPanel();
            RenderOrderTabs();
        }
        catch (Exception ex)
        {
            DetailStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DetailStatus.Text = $"Error: {ex.Message}";
        }
    }

    // ─── DELETE ORDER ───

    private async void DeleteOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int orderId) return;

        if (MessageBox.Show("Delete this order? This cannot be undone.", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            await App.OrderService!.DeleteOrderAsync(orderId);
            LoadOrderHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OrderCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not int orderId) return;
        if (cb.IsChecked == true)
            _selectedOrderIds.Add(orderId);
        else
            _selectedOrderIds.Remove(orderId);
        UpdateDeleteSelectedButton();
    }

    private void SelectAllOrders_Click(object sender, RoutedEventArgs e)
    {
        bool selectAll = SelectAllOrders.IsChecked == true;
        _selectedOrderIds.Clear();

        if (selectAll)
        {
            var searchText = OrderSearchBox.Text.Trim().ToLower();
            var statusFilter = (HistoryStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Orders";
            var orders = _allOrders.AsEnumerable();
            if (statusFilter != "All Orders")
                orders = orders.Where(o => o.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(searchText))
                orders = orders.Where(o => o.OrderNumber.ToLower().Contains(searchText)
                    || (o.Notes?.ToLower().Contains(searchText) ?? false)
                    || o.TableId?.ToString().Contains(searchText) == true);
            foreach (var o in orders)
                _selectedOrderIds.Add(o.Id);
        }

        // Update all visible checkboxes
        foreach (var child in OrderHistoryList.Children)
        {
            if (child is Border card && card.Child is Grid grid)
            {
                foreach (var element in grid.Children)
                {
                    if (element is CheckBox cb && cb.Tag is int)
                    {
                        cb.IsChecked = selectAll;
                    }
                }
            }
        }
        UpdateDeleteSelectedButton();
    }

    private void UpdateDeleteSelectedButton()
    {
        int count = _selectedOrderIds.Count;
        if (count > 0)
        {
            DeleteSelectedBtn.Visibility = Visibility.Visible;
            DeleteSelectedText.Text = $"Delete Selected ({count})";
        }
        else
        {
            DeleteSelectedBtn.Visibility = Visibility.Collapsed;
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderIds.Count == 0) return;

        int count = _selectedOrderIds.Count;
        if (MessageBox.Show($"Delete {count} selected order(s)? This cannot be undone.", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        try
        {
            foreach (var id in _selectedOrderIds.ToList())
            {
                await App.OrderService!.DeleteOrderAsync(id);
            }
            LoadOrderHistory();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
            LoadOrderHistory();
        }
    }

    // ─── PAYMENT ───

    private void ClosePayment_Click(object sender, RoutedEventArgs e)
    {
        PaymentOverlay.Visibility = Visibility.Collapsed;
    }

    private void PaymentMethod_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string method) return;

        _selectedPaymentMethod = method;
        ResetPaymentMethodHighlights();

        border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
        border.Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xfd, 0xf4));

        bool isSplit = method == "cash_transfer" || method == "cash_pos";
        bool isCashOnly = method == "cash";

        // Show/hide panels
        CashAmountPanel.Visibility = isCashOnly ? Visibility.Visible : Visibility.Collapsed;
        SplitAmountPanel.Visibility = isSplit ? Visibility.Visible : Visibility.Collapsed;
        ChangePanel.Visibility = Visibility.Collapsed;
        SplitBalancePanel.Visibility = Visibility.Collapsed;

        if (isSplit)
        {
            SplitSecondLabel.Text = method == "cash_transfer" ? "TRANSFER AMOUNT" : "POS AMOUNT";
            SplitCashBox.Text = "";
            SplitSecondBox.Text = "";
            ConfirmPaymentBtn.IsEnabled = false;
            SplitCashBox.Focus();
        }
        else if (isCashOnly)
        {
            AmountReceivedBox.Text = "";
            ConfirmPaymentBtn.IsEnabled = false;
            AmountReceivedBox.Focus();
        }
        else
        {
            // POS, Transfer — confirm immediately
            ConfirmPaymentBtn.IsEnabled = true;
        }
    }

    private void SplitAmount_Changed(object sender, TextChangedEventArgs e)
    {
        decimal cashAmt = 0, secondAmt = 0;
        decimal.TryParse(SplitCashBox.Text.Trim(), out cashAmt);
        decimal.TryParse(SplitSecondBox.Text.Trim(), out secondAmt);

        var total = _paymentTotal;
        var sum = cashAmt + secondAmt;

        if (sum >= total && cashAmt > 0 && secondAmt > 0)
        {
            var overpay = sum - total;
            if (overpay > 0)
            {
                SplitBalanceLabel.Text = "Change";
                SplitBalanceText.Text = Fmt(overpay);
            }
            else
            {
                SplitBalanceLabel.Text = "Balanced ✓";
                SplitBalanceText.Text = Fmt(0);
            }
            SplitBalancePanel.Visibility = Visibility.Visible;
            ConfirmPaymentBtn.IsEnabled = true;
        }
        else
        {
            var remaining = total - sum;
            SplitBalanceLabel.Text = "Remaining";
            SplitBalanceText.Text = Fmt(remaining > 0 ? remaining : 0);
            SplitBalancePanel.Visibility = (cashAmt > 0 || secondAmt > 0) ? Visibility.Visible : Visibility.Collapsed;
            ConfirmPaymentBtn.IsEnabled = false;
        }
    }

    private void ResetPaymentMethodHighlights()
    {
        var defaultBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));
        var whiteBrush = Brushes.White;
        foreach (var b in new[] { PayCash, PayTransfer, PayPOS, PayCashTransfer, PayCashPOS })
        {
            b.BorderBrush = defaultBrush;
            b.Background = whiteBrush;
        }
    }

    private void AmountReceived_Changed(object sender, TextChangedEventArgs e)
    {
        if (!decimal.TryParse(AmountReceivedBox.Text.Trim(), out var received))
        {
            ChangePanel.Visibility = Visibility.Collapsed;
            ConfirmPaymentBtn.IsEnabled = false;
            return;
        }

        var total = _paymentTotal;
        if (received >= total)
        {
            var change = received - total;
            ChangeText.Text = Fmt(change);
            ChangePanel.Visibility = Visibility.Visible;
            ConfirmPaymentBtn.IsEnabled = true;
        }
        else
        {
            ChangePanel.Visibility = Visibility.Collapsed;
            ConfirmPaymentBtn.IsEnabled = false;
        }
    }

    private async void ConfirmPayment_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Order savedOrder;

            if (_paymentFromHistory)
            {
                // Payment from order history detail
                if (_detailOrder == null) return;
                _detailOrder.Status = "completed";
                _detailOrder.PaymentMethod = _selectedPaymentMethod;
                savedOrder = await App.OrderService!.UpdateOrderAsync(_detailOrder);

                // Build items list from the order's existing items
                var historyItems = _detailOrder.Items.Select(oi => new OrderItemEntry
                {
                    InventoryItemId = oi.InventoryItemId ?? 0,
                    Name = oi.InventoryItem?.Name ?? "Item",
                    Price = oi.Price,
                    Quantity = oi.Quantity
                }).ToList();

                PaymentOverlay.Visibility = Visibility.Collapsed;
                PrintReceipt(savedOrder, historyItems, _selectedPaymentMethod);
                _detailOrder = null;
                LoadOrderHistory();
            }
            else
            {
                // Payment from new order tab
                var items = CurrentItems;
                if (items.Count == 0) return;

                int? tableId = null;
                if (int.TryParse(OrderTableNumber.Text.Trim(), out var tid)) tableId = tid;

                if (CurrentEditingOrderId.HasValue)
                {
                    var order = await App.OrderService!.GetOrderAsync(CurrentEditingOrderId.Value);
                    if (order == null) return;

                    order.TableId = tableId;
                    order.Notes = OrderNotes.Text.Trim();
                    order.Status = "completed";
                    order.PaymentMethod = _selectedPaymentMethod;
                    order.Total = items.Sum(i => i.Total);
                    order.Items.Clear();
                    foreach (var entry in items)
                    {
                        order.Items.Add(new OrderItem
                        {
                            InventoryItemId = entry.InventoryItemId,
                            Quantity = entry.Quantity,
                            Price = entry.Price
                        });
                    }
                    savedOrder = await App.OrderService.UpdateOrderAsync(order);
                }
                else
                {
                    var orderNumber = GenerateOrderNumber();
                    var order = new Order
                    {
                        OrderNumber = orderNumber,
                        TableId = tableId,
                        UserId = App.CurrentUser?.Id,
                        Status = "completed",
                        PaymentMethod = _selectedPaymentMethod,
                        Notes = OrderNotes.Text.Trim(),
                        Total = items.Sum(i => i.Total)
                    };
                    foreach (var entry in items)
                    {
                        order.Items.Add(new OrderItem
                        {
                            InventoryItemId = entry.InventoryItemId,
                            Quantity = entry.Quantity,
                            Price = entry.Price
                        });
                    }
                    savedOrder = await App.OrderService!.CreateOrderAsync(order);
                }

                PaymentOverlay.Visibility = Visibility.Collapsed;
                PrintReceipt(savedOrder, items, _selectedPaymentMethod);
                ResetCurrentTab();
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            MessageBox.Show($"Error: {msg}", "Payment Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintReceipt(Order order, List<OrderItemEntry> items, string paymentMethod)
    {
        var payLabel = paymentMethod switch
        {
            "cash" => "Cash",
            "transfer" => "Bank Transfer",
            "pos" => "POS",
            "cash_transfer" => "Cash & Transfer",
            "cash_pos" => "Cash & POS",
            _ => paymentMethod
        };

        const int W = 42; // receipt width in characters
        var doc = new System.Windows.Documents.FlowDocument();
        doc.FontFamily = new FontFamily("Consolas");
        doc.FontSize = 12;
        doc.PagePadding = new Thickness(20, 15, 20, 15);
        doc.ColumnWidth = double.PositiveInfinity;

        void AddText(string text, bool bold = false, TextAlignment align = TextAlignment.Left)
        {
            // Pad to W chars so all lines have identical pixel width in monospace font
            var paddedText = text.Length < W ? text + new string(' ', W - text.Length) : text;
            var run = new System.Windows.Documents.Run(paddedText);
            if (bold) run.FontWeight = FontWeights.Bold;
            var para = new System.Windows.Documents.Paragraph(run)
            {
                TextAlignment = align,
                Margin = new Thickness(0, 0, 0, 0)
            };
            doc.Blocks.Add(para);
        }

        List<string> WrapText(string text, int width)
        {
            var res = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return new List<string> { "" };
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLine = "";
            foreach (var word in words)
            {
                if ((currentLine + " " + word).Trim().Length <= width)
                    currentLine = (currentLine + " " + word).Trim();
                else
                {
                    if (!string.IsNullOrEmpty(currentLine)) res.Add(currentLine);
                    var remaining = word;
                    while (remaining.Length > width)
                    {
                        res.Add(remaining.Substring(0, width));
                        remaining = remaining.Substring(width);
                    }
                    currentLine = remaining;
                }
            }
            if (!string.IsNullOrEmpty(currentLine)) res.Add(currentLine);
            return res;
        }

        void AddRow(string name, int nameWidth, string otherCols, bool bold = false)
        {
            var lines = WrapText(name, nameWidth);
            for (int i = 0; i < lines.Count - 1; i++)
                AddText(lines[i], bold);
            
            AddText($"{lines.Last().PadRight(nameWidth)} {otherCols}", bold);
        }

        void AddSep(bool thick = false)
        {
            AddText(new string(thick ? '═' : '─', W), false, TextAlignment.Left);
        }

        string Center(string text)
        {
            if (text.Length >= W) return text;
            int pad = (W - text.Length) / 2;
            return new string(' ', pad) + text;
        }

        // === LOGO ===
        var resolvedLogoPath = AdminSettingsView.ResolveImagePath(_settings.LogoImagePath);
        if (_settings.PrintLogoOnReceipt && resolvedLogoPath != null)
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(resolvedLogoPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var img = new System.Windows.Controls.Image
                {
                    Source = bitmap,
                    Width = 50,
                    Height = 50,
                    Stretch = Stretch.Uniform
                };
                // Use InlineUIContainer in a Paragraph (same approach as text lines)
                // BlockUIContainer alignment gets ignored by the PDF paginator
                var inlineContainer = new System.Windows.Documents.InlineUIContainer(img);
                var para = new System.Windows.Documents.Paragraph(inlineContainer)
                {
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(120, 4, 0, 4)
                };
                doc.Blocks.Add(para);
            }
            catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to load logo for receipt"); }
        }

        // === HEADER ===
        AddSep(true);
        AddText(Center(_settings.BusinessNameLine1), true);
        if (!string.IsNullOrWhiteSpace(_settings.BusinessNameLine2))
            AddText(Center(_settings.BusinessNameLine2));

        if (!string.IsNullOrWhiteSpace(_settings.ReceiptHeader))
        {
            foreach (var line in _settings.ReceiptHeader.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                AddText(Center(line.Trim()));
        }
        AddSep(true);

        // === ORDER INFO ===
        AddText("");
        AddText($"Order #:   {order.OrderNumber}", true);
        AddText($"Date:      {order.CreatedAt:dd MMM yyyy  hh:mm tt}");
        if (order.TableId.HasValue)
            AddText($"Table:     #{order.TableId}");
        AddText($"Served by: {order.User?.Name ?? App.CurrentUser?.Name ?? "Staff"}");
        AddText("");
        AddSep();

        // === ITEMS ===
        AddText($" {"QTY",-4} {"ITEM",-18} {"PRICE",8} {"TOTAL",8}", true);
        AddSep();
        foreach (var item in items)
        {
            // Format: Qty (4) + name (18) + Price (8) + Total (8) = 38 chars (+ 3 separators/lead) = 42 chars
            var qtyStr = $"{item.Quantity}x";
            var otherCols = $"{Fmt(item.Price),8} {Fmt(item.Total),8}";
            
            // Name area width: 23 chars (4 for qtyStr + space + 18 for name)
            AddRow($"{qtyStr,-4} {item.Name.ToUpper()}", 23, otherCols, true);
        }
        AddText("");

        AddSep();

        // === TOTAL ===
        AddText($" {"TOTAL",-20} {Fmt(order.Total),18}", true);
        AddText("");
        AddText($" Payment: {payLabel}");

        if (paymentMethod == "cash" && decimal.TryParse(AmountReceivedBox.Text.Trim(), out var received))
        {
            AddText($" Received: {Fmt(received)}");
            AddText($" Change:   {Fmt(received - order.Total)}");
        }
        else if ((paymentMethod == "cash_transfer" || paymentMethod == "cash_pos") &&
                 decimal.TryParse(SplitCashBox.Text.Trim(), out var splitCash) &&
                 decimal.TryParse(SplitSecondBox.Text.Trim(), out var splitSecond))
        {
            var secondLabel = paymentMethod == "cash_transfer" ? "Transfer" : "POS";
            AddText($" Cash:     {Fmt(splitCash)}");
            AddText($" {secondLabel + ":",-10}{Fmt(splitSecond)}");
            var overpay = splitCash + splitSecond - order.Total;
            if (overpay > 0) AddText($" Change:   {Fmt(overpay)}");
        }

        AddSep(true);

        // === FOOTER ===
        if (!string.IsNullOrWhiteSpace(_settings.ReceiptFooter))
        {
            foreach (var line in _settings.ReceiptFooter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                AddText(Center(line.Trim()));
        }
        else
        {
            AddText(Center("Thank you for your patronage!"));
        }
        AddSep(true);

        // Intelligent Printer Selection
        try
        {
            var printDialog = new PrintDialog();
            bool shouldShowDialog = false;

            // 1. Get default printer name
            var defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
            string printerName = defaultQueue.Name.ToLower();

            // 2. Decide if we should show the dialog or print directly
            // If default is a virtual printer (PDF, XPS, etc.), show the dialog
            if (printerName.Contains("pdf") || 
                printerName.Contains("xps") || 
                printerName.Contains("onenote") || 
                printerName.Contains("anydesk") || 
                printerName.Contains("fax"))
            {
                shouldShowDialog = true;
            }

            if (shouldShowDialog && printDialog.ShowDialog() != true) return;
            
            // Clone for printing and set page size
            var printDoc = doc; 
            printDoc.ColumnWidth = double.PositiveInfinity;
            printDoc.TextAlignment = TextAlignment.Left;

            var pageWidth = printDialog.PrintableAreaWidth;
            var pageHeight = printDialog.PrintableAreaHeight;

            // Detect receipt/thermal printers
            if (pageWidth < 350)
            {
                printDoc.PagePadding = new Thickness(4, 2, 4, 2);
                printDoc.FontSize = pageWidth < 250 ? 8 : 9;
            }
            else
            {
                printDoc.PagePadding = new Thickness(20, 15, 20, 15);
            }

            var paginator = ((IDocumentPaginatorSource)printDoc).DocumentPaginator;
            paginator.PageSize = new Size(pageWidth, pageHeight);
            
            printDialog.PrintDocument(paginator, $"Receipt_{order.OrderNumber}");
        }
        catch (Exception ex)
        {
            App.Logger?.LogError(ex, "Direct print failed");
            MessageBox.Show($"Direct print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
