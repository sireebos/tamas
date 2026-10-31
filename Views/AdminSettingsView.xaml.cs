using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RestaurantManager.Models;

namespace RestaurantManager.Views;

public partial class AdminSettingsView : UserControl
{
    private BrandingSettings? _settings;
    private bool _isPopulating;
    private int _passwordEditUserId;

    public AdminSettingsView()
    {
        InitializeComponent();
        Loaded += AdminSettingsView_Loaded;
    }

    private async void AdminSettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await App.BrandingService!.GetSettingsAsync();
            _isPopulating = true;
            PopulateFields();
            _isPopulating = false;

            bool isDev = App.CurrentUser?.HasRole("developer") == true;
            bool isAdmin = App.CurrentUser?.HasRole("admin") == true;

            // Developer-only tabs
            TabBranding.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
            TabCurrency.Visibility = (isDev || isAdmin) ? Visibility.Visible : Visibility.Collapsed;
            TabReceipt.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
            TabDeveloper.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
            SidebarAdvancedLabel.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;

            // Admin + Developer tabs
            TabTheme.Visibility = (isDev || isAdmin) ? Visibility.Visible : Visibility.Collapsed;
            TabOrderSettings.Visibility = (isDev || isAdmin) ? Visibility.Visible : Visibility.Collapsed;

            // Developer role checkbox only for developers
            RoleDeveloper.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;

            SetActiveTab(isDev ? "branding" : (isAdmin ? "ordersettings" : "login"));
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Error loading settings: {ex.Message}";
        }
    }

    // ─── TAB SWITCHING ───

    private void SetActiveTab(string tab)
    {
        var inactiveFg = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));
        var activeBg = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff));
        var activeFg = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));

        // Reset all tabs
        Button[] allTabs = { TabBranding, TabLoginForm, TabCurrency, TabTheme, TabUsers,
                             TabDepartments, TabCategories, TabOrderSettings, TabReceipt, TabSidebarWelcome, TabDeveloper };
        foreach (var t in allTabs)
        {
            t.Background = Brushes.Transparent;
            t.Foreground = inactiveFg;
        }

        BrandingPanel.Visibility = Visibility.Collapsed;
        LoginFormPanel.Visibility = Visibility.Collapsed;
        CurrencyPanel.Visibility = Visibility.Collapsed;
        ThemePanel.Visibility = Visibility.Collapsed;
        UsersPanel.Visibility = Visibility.Collapsed;
        DepartmentsPanel.Visibility = Visibility.Collapsed;
        CategoriesPanel.Visibility = Visibility.Collapsed;
        OrderSettingsPanel.Visibility = Visibility.Collapsed;
        ReceiptPanel.Visibility = Visibility.Collapsed;
        SidebarWelcomePanel.Visibility = Visibility.Collapsed;
        DeveloperPanel.Visibility = Visibility.Collapsed;

        void Activate(Button btn, StackPanel panel)
        {
            btn.Background = activeBg;
            btn.Foreground = activeFg;
            panel.Visibility = Visibility.Visible;
        }

        switch (tab)
        {
            case "branding":
                Activate(TabBranding, BrandingPanel);
                break;
            case "login":
                Activate(TabLoginForm, LoginFormPanel);
                break;
            case "currency":
                Activate(TabCurrency, CurrencyPanel);
                break;
            case "theme":
                Activate(TabTheme, ThemePanel);
                break;
            case "users":
                Activate(TabUsers, UsersPanel);
                LoadUsers();
                break;
            case "departments":
                Activate(TabDepartments, DepartmentsPanel);
                LoadDepartments();
                break;
            case "categories":
                Activate(TabCategories, CategoriesPanel);
                LoadCategories();
                break;
            case "ordersettings":
                Activate(TabOrderSettings, OrderSettingsPanel);
                break;
            case "receipt":
                Activate(TabReceipt, ReceiptPanel);
                break;
            case "developer":
                Activate(TabDeveloper, DeveloperPanel);
                break;
            case "sidebar":
                Activate(TabSidebarWelcome, SidebarWelcomePanel);
                break;
        }
    }

    private void TabBranding_Click(object sender, RoutedEventArgs e) => SetActiveTab("branding");
    private void TabLoginForm_Click(object sender, RoutedEventArgs e) => SetActiveTab("login");
    private void TabCurrency_Click(object sender, RoutedEventArgs e) => SetActiveTab("currency");
    private void TabTheme_Click(object sender, RoutedEventArgs e) => SetActiveTab("theme");
    private void TabUsers_Click(object sender, RoutedEventArgs e) => SetActiveTab("users");
    private void TabDepartments_Click(object sender, RoutedEventArgs e) => SetActiveTab("departments");
    private void TabCategories_Click(object sender, RoutedEventArgs e) => SetActiveTab("categories");
    private void TabOrderSettings_Click(object sender, RoutedEventArgs e) => SetActiveTab("ordersettings");
    private void TabReceipt_Click(object sender, RoutedEventArgs e) => SetActiveTab("receipt");
    private void TabSidebarWelcome_Click(object sender, RoutedEventArgs e) => SetActiveTab("sidebar");
    private void TabDeveloper_Click(object sender, RoutedEventArgs e) => SetActiveTab("developer");

    // ─── POPULATE ───

    private void PopulateFields()
    {
        if (_settings == null) return;

        LogoEmojiBox.Text = _settings.LogoEmoji;
        LogoPreview.Text = _settings.LogoEmoji;
        NameLine1Box.Text = _settings.BusinessNameLine1;
        NameLine2Box.Text = _settings.BusinessNameLine2;
        TaglineBox.Text = _settings.Tagline;
        DescriptionBox.Text = _settings.Description;
        CopyrightBox.Text = _settings.CopyrightText;
        WindowTitleBox.Text = _settings.WindowTitle;
        WelcomeTitleBox.Text = _settings.WelcomeTitle;
        WelcomeSubtitleBox.Text = _settings.WelcomeSubtitle;

        // Currency
        CurrencySymbolBox.Text = _settings.CurrencySymbol;

        // Receipt
        ReceiptHeaderBox.Text = _settings.ReceiptHeader;
        ReceiptFooterBox.Text = _settings.ReceiptFooter;
        PrintLogoOnReceiptToggle.IsChecked = _settings.PrintLogoOnReceipt;

        // Developer
        ShowDemoLoginToggle.IsChecked = _settings.ShowDemoLogin;
        TrademarkBox.Text = _settings.TrademarkText;
        LoginButtonTextBox.Text = _settings.LoginButtonText;
        EmailLabelBox.Text = _settings.EmailLabel;
        PasswordLabelBox.Text = _settings.PasswordLabel;
        QuickLoginTitleBox.Text = _settings.QuickLoginTitle;
        PasswordHintBox.Text = _settings.PasswordHintText;
        FeaturePill1Box.Text = _settings.FeaturePill1;
        FeaturePill2Box.Text = _settings.FeaturePill2;
        FeaturePill3Box.Text = _settings.FeaturePill3;
        UpdateCheckUrlBox.Text = _settings.UpdateCheckUrl;
        PillBgColorBox.Text = _settings.FeaturePillBgColor;
        PillTextColorBox.Text = _settings.FeaturePillTextColor;
        try { PillBgColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.FeaturePillBgColor)); } catch { }
        try { PillTextColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_settings.FeaturePillTextColor)); } catch { }
        TitleBarColorBox.Text = _settings.TitleBarColor;
        UpdateColorPreview(_settings.TitleBarColor);
        TitleBarBackgroundColorBox.Text = _settings.TitleBarBackgroundColor;
        UpdateTitleBarBackgroundColorPreview(_settings.TitleBarBackgroundColor);
        UpdateCardSizeSelection(_settings.ProductCardSize ?? "medium");
        UpdateCatTabSizeSelection(_settings.CategoryTabSize ?? "medium");
        SelectComboByTag(CardFontSizeBox, (_settings.ProductCardFontSize).ToString());
        SelectComboByTag(CardFontWeightBox, _settings.ProductCardFontWeight ?? "SemiBold");
        CurrencyCodeBox.Text = _settings.CurrencyCode;
        SelectComboByTag(CurrencyPositionBox, _settings.CurrencyPosition);
        SelectComboByTag(DecimalPlacesBox, _settings.DecimalPlaces.ToString());
        SelectComboByTag(ThousandsSepBox, _settings.ThousandsSeparator);
        SelectComboByTag(DecimalSepBox, _settings.DecimalSeparator);

        // Payment methods
        var enabledMethods = (_settings.EnabledPaymentMethods ?? "cash,transfer,pos,cash_transfer,cash_pos").Split(',', StringSplitOptions.TrimEntries);
        PayMethodCash.IsChecked = enabledMethods.Contains("cash");
        PayMethodTransfer.IsChecked = enabledMethods.Contains("transfer");
        PayMethodPOS.IsChecked = enabledMethods.Contains("pos");
        PayMethodCashTransfer.IsChecked = enabledMethods.Contains("cash_transfer");
        PayMethodCashPOS.IsChecked = enabledMethods.Contains("cash_pos");

        UpdateLogoPreview(_settings.LogoImagePath);
        UpdateCurrencyPreview();

        // Sidebar & Welcome
        SidebarBgColorBox.Text = _settings.SidebarBgColor;
        SidebarTextColorBox.Text = _settings.SidebarTextColor;
        SidebarNavActiveColorBox.Text = _settings.SidebarNavActiveColor;
        SidebarBgImageLabel.Text = string.IsNullOrEmpty(_settings.SidebarBgImagePath) ? "No image selected" : System.IO.Path.GetFileName(_settings.SidebarBgImagePath);
        WelcomeBgColorBox.Text = _settings.WelcomeBgColor;
        WelcomeTextColorBox.Text = _settings.WelcomeTextColor;
        WelcomeBgImageLabel.Text = string.IsNullOrEmpty(_settings.WelcomeBgImagePath) ? "No image selected" : System.IO.Path.GetFileName(_settings.WelcomeBgImagePath);
        UpdateSidebarColorPreviews();
        UpdateWelcomeColorPreviews();

        // Theme colors
        Card1Color1Box.Text = _settings.Card1Color1;
        Card1Color2Box.Text = _settings.Card1Color2;
        Card2Color1Box.Text = _settings.Card2Color1;
        Card2Color2Box.Text = _settings.Card2Color2;
        Card3Color1Box.Text = _settings.Card3Color1;
        Card3Color2Box.Text = _settings.Card3Color2;
        Card4Color1Box.Text = _settings.Card4Color1;
        Card4Color2Box.Text = _settings.Card4Color2;
        CardTextColorBox.Text = _settings.CardTextColor;
        UpdateCardPreviews();
        UpdateCardTextPreview();

        LogoEmojiBox.TextChanged += (_, _) => LogoPreview.Text = LogoEmojiBox.Text;
    }

    private void SelectComboByTag(ComboBox combo, string tag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    // ─── CURRENCY PREVIEW ───

    private void Currency_Changed(object sender, object e)
    {
        if (_isPopulating) return;
        UpdateCurrencyPreview();
    }

    private void UpdateCurrencyPreview()
    {
        var symbol = CurrencySymbolBox?.Text ?? "₦";
        var code = CurrencyCodeBox?.Text ?? "ZAR";
        var position = (CurrencyPositionBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "before";
        var decimals = int.TryParse((DecimalPlacesBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var d) ? d : 2;

        var sample = (1234.56m).ToString($"N{decimals}");
        var preview = position == "after" ? $"{sample} {symbol}" : $"{symbol} {sample}";

        if (CurrencyPreview != null) CurrencyPreview.Text = preview;
        if (CurrencyCodePreview != null) CurrencyCodePreview.Text = code;
    }

    // ─── CURRENCY PRESETS ───

    private void CurrencyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string preset) return;

        _isPopulating = true;
        switch (preset)
        {
            case "ZAR": CurrencySymbolBox.Text = "R"; CurrencyCodeBox.Text = "ZAR"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "USD": CurrencySymbolBox.Text = "$"; CurrencyCodeBox.Text = "USD"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "EUR": CurrencySymbolBox.Text = "\u20AC"; CurrencyCodeBox.Text = "EUR"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, "."); SelectComboByTag(DecimalSepBox, ","); break;
            case "GBP": CurrencySymbolBox.Text = "\u00A3"; CurrencyCodeBox.Text = "GBP"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "JPY": CurrencySymbolBox.Text = "\u00A5"; CurrencyCodeBox.Text = "JPY"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "0"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "NGN": CurrencySymbolBox.Text = "\u20A6"; CurrencyCodeBox.Text = "NGN"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "INR": CurrencySymbolBox.Text = "\u20B9"; CurrencyCodeBox.Text = "INR"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, ","); SelectComboByTag(DecimalSepBox, "."); break;
            case "BRL": CurrencySymbolBox.Text = "R$"; CurrencyCodeBox.Text = "BRL"; SelectComboByTag(CurrencyPositionBox, "before"); SelectComboByTag(DecimalPlacesBox, "2"); SelectComboByTag(ThousandsSepBox, "."); SelectComboByTag(DecimalSepBox, ","); break;
        }
        _isPopulating = false;
        UpdateCurrencyPreview();
    }

    // ─── CARD COLOR PREVIEW ───

    private void CardColor_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isPopulating) return;
        UpdateCardPreviews();
    }

    private Color ParseHexColor(string hex, Color fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return c;
        }
        catch { return fallback; }
    }

    // ─── TITLE BAR COLOR SWATCH CLICKS ───

    private void TitleBarColorSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string hex)
        {
            TitleBarColorBox.Text = hex;
            UpdateColorPreview(hex);
        }
    }

    private void TitleBarBgColorSwatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string hex)
        {
            TitleBarBackgroundColorBox.Text = hex;
            UpdateTitleBarBackgroundColorPreview(hex);
        }
    }

    // ─── TITLE BAR COLOR PREVIEW ───

    private void TitleBarColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPopulating || TitleBarColorBox == null) return;
        UpdateColorPreview(TitleBarColorBox.Text);
    }

    private void UpdateColorPreview(string colorHex)
    {
        try
        {
            var color = ParseHexColor(colorHex, Color.FromRgb(0x66, 0x7e, 0xea));
            if (ColorPreview != null)
            {
                ColorPreview.Background = new SolidColorBrush(color);
            }
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to update color preview"); }
    }

    private void TitleBarBackgroundColorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isPopulating || TitleBarBackgroundColorBox == null) return;
        UpdateTitleBarBackgroundColorPreview(TitleBarBackgroundColorBox.Text);
    }

    private void UpdateTitleBarBackgroundColorPreview(string colorHex)
    {
        try
        {
            var color = ParseHexColor(colorHex, Color.FromRgb(0xff, 0xff, 0xff));
            if (TitleBarBackgroundColorPreview != null)
            {
                TitleBarBackgroundColorPreview.Background = new SolidColorBrush(color);
            }
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to update title bar background color preview"); }
    }

    // ─── COLOR PICKER HELPER ───
    private string? OpenColorPicker(string currentHex)
    {
        var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true
        };
        try
        {
            var c = ParseHexColor(currentHex, Color.FromRgb(0x66, 0x7e, 0xea));
            dlg.Color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch { }

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return null;
    }

    private void PickPillBgColor_Click(object sender, RoutedEventArgs e)
    {
        var hex = OpenColorPicker(PillBgColorBox.Text);
        if (hex != null)
        {
            PillBgColorBox.Text = hex;
            try { PillBgColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); } catch { }
        }
    }

    private void PickPillTextColor_Click(object sender, RoutedEventArgs e)
    {
        var hex = OpenColorPicker(PillTextColorBox.Text);
        if (hex != null)
        {
            PillTextColorBox.Text = hex;
            try { PillTextColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); } catch { }
        }
    }

    private void PickTitleBarColor_Click(object sender, RoutedEventArgs e)
    {
        var hex = OpenColorPicker(TitleBarColorBox.Text);
        if (hex != null)
        {
            TitleBarColorBox.Text = hex;
            UpdateColorPreview(hex);
        }
    }

    private void PickTitleBarBgColor_Click(object sender, RoutedEventArgs e)
    {
        var hex = OpenColorPicker(TitleBarBackgroundColorBox.Text);
        if (hex != null)
        {
            TitleBarBackgroundColorBox.Text = hex;
            UpdateTitleBarBackgroundColorPreview(hex);
        }
    }

    // ─── PRODUCT CARD SIZE ───

    private string _selectedCardSize = "medium";

    private void CardSizeOption_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string size)
        {
            _selectedCardSize = size;
            UpdateCardSizeSelection(size);
        }
    }

    private void UpdateCardSizeSelection(string size)
    {
        _selectedCardSize = size;
        var activeBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));

        foreach (var (border, tag) in new[] {
            (CardSizeSmall, "small"), (CardSizeMedium, "medium"), (CardSizeLarge, "large"),
            (CardSizeXLarge, "xlarge"), (CardSizeXXLarge, "xxlarge"), (CardSizeXXXLarge, "xxxlarge") })
        {
            border.BorderBrush = size == tag ? activeBrush : inactiveBrush;
            border.BorderThickness = new Thickness(size == tag ? 2 : 1.5);
        }
    }

    // ─── CATEGORY TAB SIZE ───

    private string _selectedCatTabSize = "medium";

    private void CatTabSizeOption_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string size)
        {
            _selectedCatTabSize = size;
            UpdateCatTabSizeSelection(size);
        }
    }

    private void UpdateCatTabSizeSelection(string size)
    {
        _selectedCatTabSize = size;
        var activeBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0));

        foreach (var (border, tag) in new[] {
            (CatTabSizeSmall, "small"), (CatTabSizeMedium, "medium"),
            (CatTabSizeLarge, "large"), (CatTabSizeXLarge, "xlarge") })
        {
            border.BorderBrush = size == tag ? activeBrush : inactiveBrush;
            border.BorderThickness = new Thickness(size == tag ? 2 : 1.5);
        }
    }

    // ─── FONT SETTINGS ───

    private void CardFontSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Ignore during initial load
        if (!IsLoaded) return;
    }

    private void CardFontWeight_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
    }

    // ─── SIDEBAR & WELCOME SCREEN ───

    private string _sidebarBgImagePath = "";
    private string _welcomeBgImagePath = "";

    private void UpdateSidebarColorPreviews()
    {
        try { SidebarBgPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SidebarBgColorBox.Text.Trim())); } catch { }
        try { SidebarTextPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SidebarTextColorBox.Text.Trim())); } catch { }
        try { SidebarNavActivePreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SidebarNavActiveColorBox.Text.Trim())); } catch { }
    }

    private void UpdateWelcomeColorPreviews()
    {
        try { WelcomeBgPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(WelcomeBgColorBox.Text.Trim())); } catch { }
        try { WelcomeTextPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(WelcomeTextColorBox.Text.Trim())); } catch { }
    }

    private void SidebarColorPreview_Changed(object sender, TextChangedEventArgs e) => UpdateSidebarColorPreviews();
    private void WelcomeColorPreview_Changed(object sender, TextChangedEventArgs e) => UpdateWelcomeColorPreviews();

    private void SidebarBgImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Title = "Select Sidebar Background Image"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var dest = Path.Combine(GetImagesStorageDir(), "sidebar_bg" + Path.GetExtension(dlg.FileName));
                File.Copy(dlg.FileName, dest, overwrite: true);
                _sidebarBgImagePath = dest;
                SidebarBgImageLabel.Text = System.IO.Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage.Text = $"Error copying sidebar image: {ex.Message}";
            }
        }
    }

    private void SidebarBgImage_Clear(object sender, RoutedEventArgs e)
    {
        _sidebarBgImagePath = "__CLEAR__";
        SidebarBgImageLabel.Text = "No image selected";
    }

    private void WelcomeBgImage_Browse(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*",
            Title = "Select Welcome Screen Background Image"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var dest = Path.Combine(GetImagesStorageDir(), "welcome_bg" + Path.GetExtension(dlg.FileName));
                File.Copy(dlg.FileName, dest, overwrite: true);
                _welcomeBgImagePath = dest;
                WelcomeBgImageLabel.Text = System.IO.Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage.Text = $"Error copying welcome image: {ex.Message}";
            }
        }
    }

    private void WelcomeBgImage_Clear(object sender, RoutedEventArgs e)
    {
        _welcomeBgImagePath = "__CLEAR__";
        WelcomeBgImageLabel.Text = "No image selected";
    }

    private void ResetSidebarDefaults_Click(object sender, RoutedEventArgs e)
    {
        SidebarBgColorBox.Text = "#f8fafc";
        SidebarTextColorBox.Text = "#334155";
        SidebarNavActiveColorBox.Text = "#667eea";
        _sidebarBgImagePath = "__CLEAR__";
        SidebarBgImageLabel.Text = "No image selected";
        WelcomeBgColorBox.Text = "#ffffff";
        WelcomeTextColorBox.Text = "#0f172a";
        _welcomeBgImagePath = "__CLEAR__";
        WelcomeBgImageLabel.Text = "No image selected";
        UpdateSidebarColorPreviews();
        UpdateWelcomeColorPreviews();
    }

    // ─── CARD PREVIEW CONTINUED ───

    private void UpdateCardPreviews()
    {
        if (Card1Color1Box == null) return;
        Card1Stop1.Color = ParseHexColor(Card1Color1Box.Text, Color.FromRgb(0x66, 0x7e, 0xea));
        Card1Stop2.Color = ParseHexColor(Card1Color2Box.Text, Color.FromRgb(0x76, 0x4b, 0xa2));
        Card2Stop1.Color = ParseHexColor(Card2Color1Box.Text, Color.FromRgb(0xf9, 0x73, 0x16));
        Card2Stop2.Color = ParseHexColor(Card2Color2Box.Text, Color.FromRgb(0xea, 0x58, 0x0c));
        Card3Stop1.Color = ParseHexColor(Card3Color1Box.Text, Color.FromRgb(0x10, 0xb9, 0x81));
        Card3Stop2.Color = ParseHexColor(Card3Color2Box.Text, Color.FromRgb(0x05, 0x96, 0x69));
        Card4Stop1.Color = ParseHexColor(Card4Color1Box.Text, Color.FromRgb(0x8b, 0x5c, 0xf6));
        Card4Stop2.Color = ParseHexColor(Card4Color2Box.Text, Color.FromRgb(0x6d, 0x28, 0xd9));
    }

    private void ThemePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string preset) return;
        _isPopulating = true;
        switch (preset)
        {
            case "default":
                Card1Color1Box.Text = "#667eea"; Card1Color2Box.Text = "#764ba2";
                Card2Color1Box.Text = "#f97316"; Card2Color2Box.Text = "#ea580c";
                Card3Color1Box.Text = "#10b981"; Card3Color2Box.Text = "#059669";
                Card4Color1Box.Text = "#8b5cf6"; Card4Color2Box.Text = "#6d28d9";
                break;
            case "ocean":
                Card1Color1Box.Text = "#0ea5e9"; Card1Color2Box.Text = "#0369a1";
                Card2Color1Box.Text = "#06b6d4"; Card2Color2Box.Text = "#0891b2";
                Card3Color1Box.Text = "#14b8a6"; Card3Color2Box.Text = "#0d9488";
                Card4Color1Box.Text = "#6366f1"; Card4Color2Box.Text = "#4f46e5";
                break;
            case "sunset":
                Card1Color1Box.Text = "#f43f5e"; Card1Color2Box.Text = "#e11d48";
                Card2Color1Box.Text = "#f97316"; Card2Color2Box.Text = "#ea580c";
                Card3Color1Box.Text = "#eab308"; Card3Color2Box.Text = "#ca8a04";
                Card4Color1Box.Text = "#ec4899"; Card4Color2Box.Text = "#db2777";
                break;
            case "forest":
                Card1Color1Box.Text = "#22c55e"; Card1Color2Box.Text = "#16a34a";
                Card2Color1Box.Text = "#84cc16"; Card2Color2Box.Text = "#65a30d";
                Card3Color1Box.Text = "#10b981"; Card3Color2Box.Text = "#059669";
                Card4Color1Box.Text = "#14b8a6"; Card4Color2Box.Text = "#0d9488";
                break;
            case "candy":
                Card1Color1Box.Text = "#ec4899"; Card1Color2Box.Text = "#db2777";
                Card2Color1Box.Text = "#a855f7"; Card2Color2Box.Text = "#9333ea";
                Card3Color1Box.Text = "#f472b6"; Card3Color2Box.Text = "#ec4899";
                Card4Color1Box.Text = "#8b5cf6"; Card4Color2Box.Text = "#7c3aed";
                break;
            case "midnight":
                Card1Color1Box.Text = "#6366f1"; Card1Color2Box.Text = "#4338ca";
                Card2Color1Box.Text = "#8b5cf6"; Card2Color2Box.Text = "#6d28d9";
                Card3Color1Box.Text = "#a78bfa"; Card3Color2Box.Text = "#7c3aed";
                Card4Color1Box.Text = "#818cf8"; Card4Color2Box.Text = "#6366f1";
                break;
        }
        _isPopulating = false;
        UpdateCardPreviews();
    }

    // ─── LOGO ───

    private void UpdateLogoPreview(string? imagePath)
    {
        var resolved = ResolveImagePath(imagePath);
        if (resolved != null)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(resolved, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                LogoImagePreview.Source = bmp;
                LogoImagePreview.Visibility = Visibility.Visible;
                LogoPreview.Visibility = Visibility.Collapsed;
                RemoveLogoBtn.Visibility = Visibility.Visible;
                LogoPathLabel.Text = Path.GetFileName(imagePath);
                return;
            }
            catch { /* fall through to default emoji */ }
        }

        LogoImagePreview.Source = null;
        LogoImagePreview.Visibility = Visibility.Collapsed;
        LogoPreview.Visibility = Visibility.Visible;
        RemoveLogoBtn.Visibility = Visibility.Collapsed;
        LogoPathLabel.Text = "";
    }

    private static string GetLogoStorageDir()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "logos");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetImagesStorageDir()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", "images");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Resolves an image path stored in the DB. If the absolute path exists, use it.
    /// If not (e.g. different PC/user), try to find the file by name under the current AppData.
    /// </summary>
    internal static string? ResolveImagePath(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath)) return null;
        if (File.Exists(storedPath)) return storedPath;

        var fileName = Path.GetFileName(storedPath);

        // 1. Try new local storage path
        foreach (var subDir in new[] { "logos", "images" })
        {
            var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage", subDir, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        // 2. Try old AppData fallback for existing files
        var appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RestaurantManager");
        foreach (var subDir in new[] { "logos", "images" })
        {
            var candidate = Path.Combine(appDataRoot, subDir, fileName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private async void UploadLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Logo Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var dest = Path.Combine(GetLogoStorageDir(), "logo" + Path.GetExtension(dlg.FileName));
                File.Copy(dlg.FileName, dest, overwrite: true);
                if (_settings != null)
                {
                    _settings.LogoImagePath = dest;
                    await App.BrandingService!.SaveSettingsAsync(_settings);
                }
                UpdateLogoPreview(dest);
                App.ApplyLogoIcon(Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                StatusMessage.Text = $"Error uploading logo: {ex.Message}";
            }
        }
    }

    private async void RemoveLogo_Click(object sender, RoutedEventArgs e)
    {
        if (_settings != null)
        {
            _settings.LogoImagePath = null;
            await App.BrandingService!.SaveSettingsAsync(_settings);
        }
        UpdateLogoPreview(null);
        if (Window.GetWindow(this) is Window w)
            w.Icon = new BitmapImage(new Uri("pack://application:,,,/app.ico"));
    }

    // ─── PAYMENT METHODS ───

    private string BuildEnabledPaymentMethods()
    {
        var methods = new List<string>();
        if (PayMethodCash.IsChecked == true) methods.Add("cash");
        if (PayMethodTransfer.IsChecked == true) methods.Add("transfer");
        if (PayMethodPOS.IsChecked == true) methods.Add("pos");
        if (PayMethodCashTransfer.IsChecked == true) methods.Add("cash_transfer");
        if (PayMethodCashPOS.IsChecked == true) methods.Add("cash_pos");
        return string.Join(",", methods);
    }

    private async void PayMethodToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isPopulating || _settings == null) return;
        try
        {
            _settings.EnabledPaymentMethods = BuildEnabledPaymentMethods();
            await App.BrandingService!.SaveSettingsAsync(_settings);
            StatusMessage.Text = "\u2713 Payment methods updated.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    // ─── CARD TEXT COLOR ───

    private void CardTextColor_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isPopulating) return;
        UpdateCardTextPreview();
    }

    private void UpdateCardTextPreview()
    {
        if (CardTextColorBox == null || CardTextPreviewText == null) return;
        var color = ParseHexColor(CardTextColorBox.Text, Colors.White);
        CardTextPreviewText.Foreground = new SolidColorBrush(color);
    }

    // ─── USER MANAGEMENT ───

    private async void LoadUsers()
    {
        try
        {
            var users = await App.UserService!.GetAllUsersAsync();
            // Hide developer accounts from non-developer users
            var isDev = App.CurrentUser?.HasRole("developer") == true;
            var visibleUsers = isDev ? users : users.Where(u => !u.HasRole("developer")).ToList();
            UsersListPanel.Children.Clear();
            foreach (var user in visibleUsers)
            {
                var card = CreateUserCard(user);
                UsersListPanel.Children.Add(card);
            }

            // Dynamically populate department dropdown
            var departments = await App.DepartmentService!.GetAllAsync();
            NewUserDepartment.Items.Clear();
            NewUserDepartment.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "— None —", IsSelected = true });
            foreach (var dept in departments)
            {
                NewUserDepartment.Items.Add(new System.Windows.Controls.ComboBoxItem
                {
                    Content = $"{dept.Icon} {dept.Name}",
                    Tag = dept.Name.ToLower()
                });
            }
        }
        catch (Exception ex)
        {
            UserStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private Border CreateUserCard(User user)
    {
        var roleText = string.Join(", ", (user.Role ?? "staff").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(r => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(r)));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        // Avatar
        var avatar = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = new LinearGradientBrush(
                new GradientStopCollection { new GradientStop(Color.FromRgb(0x66, 0x7e, 0xea), 0), new GradientStop(Color.FromRgb(0x76, 0x4b, 0xa2), 1) },
                new Point(0, 0), new Point(1, 1)),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock { Text = "👤", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(avatar, 0);
        grid.Children.Add(avatar);

        // Info
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = user.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)) });
        info.Children.Add(new TextBlock { Text = $"{user.Email}  •  {roleText}", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)) });
        if (!string.IsNullOrEmpty(user.Department))
            info.Children.Add(new TextBlock { Text = $"Dept: {user.Department}", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)) });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        // Action buttons panel
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Password edit button
        var pwBtn = new Button
        {
            Content = "🔑", FontSize = 14, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf9, 0xc3)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = user.Id, ToolTip = "Change password",
            Margin = new Thickness(0, 0, 6, 0)
        };
        pwBtn.Click += EditPassword_Click;
        actionsPanel.Children.Add(pwBtn);

        // Delete button (don't allow deleting current user)
        if (App.CurrentUser?.Id != user.Id)
        {
            var delBtn = new Button
            {
                Content = "🗑", FontSize = 14, Width = 34, Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2)),
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Tag = user.Id, ToolTip = "Delete user"
            };
            delBtn.Click += DeleteUser_Click;
            actionsPanel.Children.Add(delBtn);
        }

        Grid.SetColumn(actionsPanel, 2);
        grid.Children.Add(actionsPanel);

        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Child = grid
        };
    }

    private async void CreateUser_Click(object sender, RoutedEventArgs e)
    {
        var name = NewUserName.Text.Trim();
        var email = NewUserEmail.Text.Trim();
        var password = NewUserPassword.Password.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            UserStatusMessage.Text = "Name, email, and password are required.";
            return;
        }

        // Build roles
        var roles = new List<string>();
        if (RoleStaff.IsChecked == true) roles.Add("staff");
        if (RoleAdmin.IsChecked == true) roles.Add("admin");
        if (RoleDeveloper.IsChecked == true) roles.Add("developer");
        if (roles.Count == 0) roles.Add("staff");

        var deptItem = NewUserDepartment.SelectedItem as ComboBoxItem;
        var dept = deptItem?.Tag?.ToString();
        if (string.IsNullOrEmpty(dept)) dept = null;

        try
        {
            var user = new User
            {
                Name = name,
                Email = email,
                Password = password,
                Role = string.Join(",", roles),
                Department = dept
            };
            await App.UserService!.CreateUserAsync(user);

            // Clear form
            NewUserName.Text = "";
            NewUserEmail.Text = "";
            NewUserPassword.Password = "";
            NewUserDepartment.SelectedIndex = 0;
            RoleStaff.IsChecked = true;
            RoleAdmin.IsChecked = false;
            RoleDeveloper.IsChecked = false;

            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            UserStatusMessage.Text = $"✓ User \"{name}\" created!";
            LoadUsers();
        }
        catch (Exception ex)
        {
            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            UserStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int userId) return;

        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Delete",
            "Delete this user? This cannot be undone.");
        if (!confirmed) return;

        try
        {
            await App.UserService!.DeleteUserAsync(userId);
            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            UserStatusMessage.Text = "✓ User deleted.";
            LoadUsers();
        }
        catch (Exception ex)
        {
            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            UserStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void EditPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int userId) return;

        var users = await App.UserService!.GetAllUsersAsync();
        var user = users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return;

        _passwordEditUserId = userId;
        PwOverlayUserName.Text = $"For: {user.Name} ({user.Email})";
        OverlayNewPassword.Password = "";
        OverlayConfirmPassword.Password = "";
        PwOverlayStatus.Text = "";
        ChangePasswordOverlay.Visibility = Visibility.Visible;
    }

    private void ClosePasswordOverlay_Click(object sender, RoutedEventArgs e)
    {
        ChangePasswordOverlay.Visibility = Visibility.Collapsed;
    }

    private void OverlayPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            SavePasswordOverlay_Click(sender, e);
        }
    }

    private async void SavePasswordOverlay_Click(object sender, RoutedEventArgs e)
    {
        var newPw = OverlayNewPassword.Password;
        var confirmPw = OverlayConfirmPassword.Password;

        if (string.IsNullOrEmpty(newPw) || string.IsNullOrEmpty(confirmPw))
        {
            PwOverlayStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            PwOverlayStatus.Text = "Both fields required.";
            return;
        }

        if (newPw.Length < 6)
        {
            PwOverlayStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            PwOverlayStatus.Text = "Min 6 characters.";
            return;
        }

        if (newPw != confirmPw)
        {
            PwOverlayStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            PwOverlayStatus.Text = "Passwords do not match.";
            return;
        }

        try
        {
            var user = await App.DbContext!.Users.FindAsync(_passwordEditUserId);
            if (user == null)
            {
                PwOverlayStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
                PwOverlayStatus.Text = "User not found.";
                return;
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(newPw);
            user.UpdatedAt = DateTime.Now;
            await App.DbContext.SaveChangesAsync();

            ChangePasswordOverlay.Visibility = Visibility.Collapsed;

            UserStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            UserStatusMessage.Text = $"✓ Password updated for {user.Name}!";
        }
        catch (Exception ex)
        {
            PwOverlayStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            PwOverlayStatus.Text = $"Error: {ex.Message}";
        }
    }

    // ─── SAVE ───

    // ─── DEPARTMENT MANAGEMENT ───

    private async void LoadDepartments()
    {
        try
        {
            var departments = await App.DepartmentService!.GetAllAsync();
            var allUsersRaw = await App.UserService!.GetAllUsersAsync();
            // Hide developer accounts from non-developer users
            var isDev = App.CurrentUser?.HasRole("developer") == true;
            var allUsers = isDev ? allUsersRaw : allUsersRaw.Where(u => !u.HasRole("developer")).ToList();
            DeptListPanel.Children.Clear();
            foreach (var dept in departments)
            {
                var card = await CreateDepartmentCard(dept, allUsers);
                DeptListPanel.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async Task<Border> CreateDepartmentCard(Department dept, List<User> allUsers)
    {
        var assignedUsers = await App.DepartmentService!.GetUserDepartmentsAsync(dept.Id);
        var assignedIds = assignedUsers.Select(ud => ud.UserId).ToHashSet();

        var mainStack = new StackPanel();

        // Header row
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var deptColor = ParseHexColor(dept.Color, Color.FromRgb(0x66, 0x7e, 0xea));
        var iconBorder = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(deptColor),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock { Text = dept.Icon, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(iconBorder, 0);
        headerGrid.Children.Add(iconBorder);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock { Text = dept.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)) });
        infoStack.Children.Add(new TextBlock { Text = $"{assignedUsers.Count} user(s) assigned", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)) });
        Grid.SetColumn(infoStack, 1);
        headerGrid.Children.Add(infoStack);

        // Action buttons (edit + delete)
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var editBtn = new Button
        {
            Content = "✏", FontSize = 13, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = dept, ToolTip = "Edit department", Margin = new Thickness(0, 0, 6, 0)
        };
        editBtn.Click += EditDept_Click;
        actionsPanel.Children.Add(editBtn);

        var delBtn = new Button
        {
            Content = "🗑", FontSize = 14, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = dept.Id, ToolTip = "Delete department"
        };
        delBtn.Click += DeleteDept_Click;
        actionsPanel.Children.Add(delBtn);

        Grid.SetColumn(actionsPanel, 2);
        headerGrid.Children.Add(actionsPanel);

        mainStack.Children.Add(headerGrid);

        // Inline edit panel (hidden by default)
        var editPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1),
            Tag = "editPanel_dept_" + dept.Id
        };
        var editGrid = new Grid();
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var editNameBox = new TextBox
        {
            Text = dept.Name, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editName"
        };
        Grid.SetColumn(editNameBox, 0);
        editGrid.Children.Add(editNameBox);

        var editIconBox = new TextBox
        {
            Text = dept.Icon, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editIcon"
        };
        Grid.SetColumn(editIconBox, 2);
        editGrid.Children.Add(editIconBox);

        var editColorBox = new TextBox
        {
            Text = dept.Color, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editColor"
        };
        Grid.SetColumn(editColorBox, 4);
        editGrid.Children.Add(editColorBox);

        var saveEditBtn = new Button
        {
            Content = "💾 Save", FontSize = 12, Height = 34, Padding = new Thickness(14, 0, 14, 0),
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)),
            Tag = new object[] { dept.Id, editNameBox, editIconBox, editColorBox }
        };
        saveEditBtn.Click += SaveEditDept_Click;
        Grid.SetColumn(saveEditBtn, 6);
        editGrid.Children.Add(saveEditBtn);

        var cancelEditBtn = new Button
        {
            Content = "✕", FontSize = 12, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)),
            Tag = editPanel
        };
        cancelEditBtn.Click += (s, _) =>
        {
            if (s is Button b && b.Tag is Border panel)
                panel.Visibility = Visibility.Collapsed;
        };
        Grid.SetColumn(cancelEditBtn, 8);
        editGrid.Children.Add(cancelEditBtn);

        editPanel.Child = editGrid;
        mainStack.Children.Add(editPanel);

        // Store reference so edit button can toggle it
        editBtn.Tag = new object[] { dept, editPanel };

        // Assigned users chips
        if (assignedUsers.Count > 0)
        {
            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var ud in assignedUsers)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff)),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 6, 5),
                    Margin = new Thickness(0, 0, 6, 6)
                };
                var chipStack = new StackPanel { Orientation = Orientation.Horizontal };
                chipStack.Children.Add(new TextBlock { Text = ud.User?.Name ?? "Unknown", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), VerticalAlignment = VerticalAlignment.Center });
                var removeBtn = new Button
                {
                    Content = "✕", FontSize = 10, Width = 20, Height = 20, Padding = new Thickness(0),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                    Tag = new int[] { dept.Id, ud.UserId }
                };
                removeBtn.Click += RemoveUserFromDept_Click;
                chipStack.Children.Add(removeBtn);
                chip.Child = chipStack;
                chipsWrap.Children.Add(chip);
            }
            mainStack.Children.Add(chipsWrap);
        }

        // Add user combo
        var unassigned = allUsers.Where(u => !assignedIds.Contains(u.Id)).ToList();
        if (unassigned.Count > 0)
        {
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var combo = new ComboBox
            {
                Width = 220, Padding = new Thickness(10, 7, 10, 7), FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                Tag = dept.Id
            };
            combo.Items.Add(new ComboBoxItem { Content = "— Select user to add —", IsSelected = true });
            foreach (var u in unassigned)
                combo.Items.Add(new ComboBoxItem { Content = u.Name, Tag = u.Id });
            addRow.Children.Add(combo);

            var addBtn = new Button
            {
                Content = "＋ Add", FontSize = 12, Height = 32, Padding = new Thickness(16, 0, 16, 0),
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(deptColor),
                Tag = combo
            };
            addBtn.Click += AssignUserToDept_Click;
            addRow.Children.Add(addBtn);
            mainStack.Children.Add(addRow);
        }

        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20, 16, 20, 16), Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Child = mainStack
        };
    }

    private async void CreateDept_Click(object sender, RoutedEventArgs e)
    {
        var name = NewDeptName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = "Department name is required.";
            return;
        }

        try
        {
            var dept = new Department
            {
                Name = name,
                Icon = NewDeptIcon.Text.Trim(),
                Color = NewDeptColor.Text.Trim()
            };
            await App.DepartmentService!.CreateAsync(dept);
            NewDeptName.Text = "";
            NewDeptIcon.Text = "🏢";
            NewDeptColor.Text = "#667eea";
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            DeptStatusMessage.Text = $"✓ Department \"{name}\" created!";
            LoadDepartments();
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void DeleteDept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int deptId) return;

        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Delete",
            "Delete this department and all user assignments? This cannot be undone.");
        if (!confirmed) return;

        try
        {
            await App.DepartmentService!.DeleteAsync(deptId);
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            DeptStatusMessage.Text = "✓ Department deleted.";
            LoadDepartments();
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private void EditDept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not object[] data || data.Length < 2) return;
        if (data[1] is Border editPanel)
        {
            editPanel.Visibility = editPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private async void SaveEditDept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not object[] data || data.Length < 4) return;
        if (data[0] is not int deptId) return;

        var nameBox = data[1] as TextBox;
        var iconBox = data[2] as TextBox;
        var colorBox = data[3] as TextBox;

        var newName = nameBox?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(newName))
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = "Department name cannot be empty.";
            return;
        }

        try
        {
            var updated = new Department
            {
                Id = deptId,
                Name = newName,
                Icon = iconBox?.Text.Trim() ?? "🏢",
                Color = colorBox?.Text.Trim() ?? "#667eea"
            };
            await App.DepartmentService!.UpdateAsync(updated);
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            DeptStatusMessage.Text = $"✓ Department \"{newName}\" updated!";
            LoadDepartments();
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void AssignUserToDept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ComboBox combo) return;
        if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not int userId) return;
        if (combo.Tag is not int deptId) return;

        try
        {
            await App.DepartmentService!.AssignUserAsync(deptId, userId);
            LoadDepartments();
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void RemoveUserFromDept_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int[] ids || ids.Length < 2) return;

        try
        {
            await App.DepartmentService!.RemoveUserAsync(ids[0], ids[1]);
            LoadDepartments();
        }
        catch (Exception ex)
        {
            DeptStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            DeptStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    // ─── CATEGORY MANAGEMENT ───

    private async void LoadCategories()
    {
        try
        {
            await App.CategoryService!.SyncMissingCategoriesAsync();
            var categories = await App.CategoryService!.GetAllAsync();
            var allDepartments = await App.DepartmentService!.GetAllAsync();
            var allSubCategories = await App.SubCategoryService!.GetAllAsync();
            CatListPanel.Children.Clear();
            foreach (var cat in categories)
            {
                var card = await CreateCategoryCard(cat, allDepartments, allSubCategories);
                CatListPanel.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async Task<Border> CreateCategoryCard(Category cat, List<Department> allDepartments, List<SubCategory> allSubCategories)
    {
        var assignedDepts = await App.CategoryDepartmentService!.GetDepartmentsForCategoryAsync(cat.Id);
        var assignedDeptIds = assignedDepts.Select(cd => cd.DepartmentId).ToHashSet();
        var subCats = await App.SubCategoryService!.GetByCategoryAsync(cat.Id);

        // Fetch item counts
        var (invCount, menuCount) = await App.CategoryService!.GetItemCountsForCategoryAsync(cat.Name);
        var totalItems = invCount + menuCount;

        var mainStack = new StackPanel();

        // Header row
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var catColor = ParseHexColor(cat.Color, Color.FromRgb(0x66, 0x7e, 0xea));
        var iconBorder = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(catColor),
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock { Text = cat.Icon, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(iconBorder, 0);
        headerGrid.Children.Add(iconBorder);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(new TextBlock { Text = cat.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)) });

        // Build item count detail string
        var detailParts = new List<string>();
        detailParts.Add($"{assignedDepts.Count} dept(s)");
        if (totalItems > 0)
        {
            var itemParts = new List<string>();
            if (invCount > 0) itemParts.Add($"{invCount} inventory");
            if (menuCount > 0) itemParts.Add($"{menuCount} menu");
            detailParts.Add($"{totalItems} item{(totalItems != 1 ? "s" : "")} ({string.Join(", ", itemParts)})");
        }
        else
        {
            detailParts.Add("0 items");
        }
        infoStack.Children.Add(new TextBlock { Text = string.Join(" · ", detailParts), FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)) });
        Grid.SetColumn(infoStack, 1);
        headerGrid.Children.Add(infoStack);

        // Action buttons (edit + delete)
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var editBtn = new Button
        {
            Content = "✏", FontSize = 13, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xf0, 0xf4, 0xff)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = cat, ToolTip = "Edit category", Margin = new Thickness(0, 0, 6, 0)
        };
        editBtn.Click += EditCat_Click;
        actionsPanel.Children.Add(editBtn);

        var delBtn = new Button
        {
            Content = "🗑", FontSize = 14, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xfe, 0xf2, 0xf2)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Tag = cat, ToolTip = "Delete category"
        };
        delBtn.Click += DeleteCat_Click;
        actionsPanel.Children.Add(delBtn);

        Grid.SetColumn(actionsPanel, 2);
        headerGrid.Children.Add(actionsPanel);

        mainStack.Children.Add(headerGrid);

        // Inline edit panel (hidden by default)
        var editPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 12, 0, 0), Visibility = Visibility.Collapsed,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1),
            Tag = "editPanel_" + cat.Id
        };
        var editGrid = new Grid();
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
        editGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var editNameBox = new TextBox
        {
            Text = cat.Name, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editName"
        };
        Grid.SetColumn(editNameBox, 0);
        editGrid.Children.Add(editNameBox);

        var editIconBox = new TextBox
        {
            Text = cat.Icon, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editIcon"
        };
        Grid.SetColumn(editIconBox, 2);
        editGrid.Children.Add(editIconBox);

        var editColorBox = new TextBox
        {
            Text = cat.Color, FontSize = 13, Padding = new Thickness(10, 8, 10, 8),
            Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Tag = "editColor"
        };
        Grid.SetColumn(editColorBox, 4);
        editGrid.Children.Add(editColorBox);

        var saveEditBtn = new Button
        {
            Content = "💾 Save", FontSize = 12, Height = 34, Padding = new Thickness(14, 0, 14, 0),
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)),
            Tag = new object[] { cat.Id, editNameBox, editIconBox, editColorBox }
        };
        saveEditBtn.Click += SaveEditCat_Click;
        Grid.SetColumn(saveEditBtn, 6);
        editGrid.Children.Add(saveEditBtn);

        var cancelEditBtn = new Button
        {
            Content = "✕", FontSize = 12, Width = 34, Height = 34,
            Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)),
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)),
            Tag = editPanel
        };
        cancelEditBtn.Click += (s, _) =>
        {
            if (s is Button b && b.Tag is Border panel)
                panel.Visibility = Visibility.Collapsed;
        };
        Grid.SetColumn(cancelEditBtn, 8);
        editGrid.Children.Add(cancelEditBtn);

        editPanel.Child = editGrid;
        mainStack.Children.Add(editPanel);

        // Store reference so edit button can toggle it
        editBtn.Tag = new object[] { cat, editPanel };

        // Assigned department chips
        if (assignedDepts.Count > 0)
        {
            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var cd in assignedDepts)
            {
                var deptColor = ParseHexColor(cd.Department?.Color ?? "#667eea", Color.FromRgb(0x66, 0x7e, 0xea));
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x20, deptColor.R, deptColor.G, deptColor.B)),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 6, 5),
                    Margin = new Thickness(0, 0, 6, 6)
                };
                var chipStack = new StackPanel { Orientation = Orientation.Horizontal };
                chipStack.Children.Add(new TextBlock { Text = cd.Department?.Icon ?? "🏢", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
                chipStack.Children.Add(new TextBlock { Text = cd.Department?.Name ?? "Unknown", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), VerticalAlignment = VerticalAlignment.Center });
                var removeBtn = new Button
                {
                    Content = "✕", FontSize = 10, Width = 20, Height = 20, Padding = new Thickness(0),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0),
                    Tag = new int[] { cat.Id, cd.DepartmentId }
                };
                removeBtn.Click += RemoveDeptFromCat_Click;
                chipStack.Children.Add(removeBtn);
                chip.Child = chipStack;
                chipsWrap.Children.Add(chip);
            }
            mainStack.Children.Add(chipsWrap);
        }

        // Add department combo
        var unassigned = allDepartments.Where(d => !assignedDeptIds.Contains(d.Id)).ToList();
        if (unassigned.Count > 0)
        {
            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var combo = new ComboBox
            {
                Width = 220, Padding = new Thickness(10, 7, 10, 7), FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                Tag = cat.Id
            };
            combo.Items.Add(new ComboBoxItem { Content = "— Select department to assign —", IsSelected = true });
            foreach (var d in unassigned)
                combo.Items.Add(new ComboBoxItem { Content = $"{d.Icon} {d.Name}", Tag = d.Id });
            addRow.Children.Add(combo);

            var addBtn = new Button
            {
                Content = "＋ Assign", FontSize = 12, Height = 32, Padding = new Thickness(16, 0, 16, 0),
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                Background = new SolidColorBrush(catColor),
                Tag = combo
            };
            addBtn.Click += AssignDeptToCat_Click;
            addRow.Children.Add(addBtn);
            mainStack.Children.Add(addRow);
        }

        // Sub-categories section
        mainStack.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0xf1, 0xf5, 0xf9)), Height = 1, Margin = new Thickness(0, 16, 0, 16) });
        mainStack.Children.Add(new TextBlock { Text = "SUB-CATEGORIES", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)), Margin = new Thickness(0, 0, 0, 10) });

        if (subCats.Count > 0)
        {
            var subCatsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var sc in subCats)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 5, 6, 5),
                    Margin = new Thickness(0, 0, 6, 6)
                };
                var chipStack = new StackPanel { Orientation = Orientation.Horizontal };
                var nameText = new TextBlock { Text = sc.Name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), VerticalAlignment = VerticalAlignment.Center };
                chipStack.Children.Add(nameText);

                var editSubBtn = new Button
                {
                    Content = "✏", FontSize = 10, Width = 20, Height = 20, Padding = new Thickness(0),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x7e, 0xea)),
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0),
                    Tag = sc, ToolTip = "Edit sub-category"
                };
                editSubBtn.Click += EditSubCat_Click;
                chipStack.Children.Add(editSubBtn);

                var removeBtn = new Button
                {
                    Content = "✕", FontSize = 10, Width = 20, Height = 20, Padding = new Thickness(0),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(2, 0, 0, 0),
                    Tag = sc.Id, ToolTip = "Delete sub-category"
                };
                removeBtn.Click += DeleteSubCat_Click;
                chipStack.Children.Add(removeBtn);
                chip.Child = chipStack;
                subCatsWrap.Children.Add(chip);
            }
            mainStack.Children.Add(subCatsWrap);
        }

        var addSubRow = new Grid();
        addSubRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addSubRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
        addSubRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var subNameBox = new TextBox
        {
            FontSize = 12, Padding = new Thickness(10, 7, 10, 7),
            Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            Text = "Add new sub-category...", Foreground = Brushes.Gray
        };
        subNameBox.GotFocus += (s, _) => { if (subNameBox.Text == "Add new sub-category...") { subNameBox.Text = ""; subNameBox.Foreground = Brushes.Black; } };
        subNameBox.LostFocus += (s, _) => { if (string.IsNullOrWhiteSpace(subNameBox.Text)) { subNameBox.Text = "Add new sub-category..."; subNameBox.Foreground = Brushes.Gray; } };
        
        Grid.SetColumn(subNameBox, 0);
        addSubRow.Children.Add(subNameBox);

        var addSubBtn = new Button
        {
            Content = "＋ Add New", FontSize = 12, Height = 32, Padding = new Thickness(16, 0, 16, 0),
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
            Background = new SolidColorBrush(catColor),
            Tag = new object[] { cat.Id, subNameBox }
        };
        addSubBtn.Click += CreateSubCat_Click;
        Grid.SetColumn(addSubBtn, 2);
        addSubRow.Children.Add(addSubBtn);
        mainStack.Children.Add(addSubRow);

        // Assign Existing Sub-category Row
        var othersSubCats = allSubCategories.Where(s => s.CategoryId != cat.Id).OrderBy(s => s.Name).ToList();
        if (othersSubCats.Count > 0)
        {
            var assignExistingRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            assignExistingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            assignExistingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Pixel) });
            assignExistingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var existingSubCombo = new ComboBox
            {
                FontSize = 12, Padding = new Thickness(10, 7, 10, 7),
                Background = new SolidColorBrush(Color.FromRgb(0xf8, 0xfa, 0xfc)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
                Tag = cat.Id
            };
            existingSubCombo.Items.Add(new ComboBoxItem { Content = "— Reassign existing sub-category —", IsSelected = true });
            foreach (var s in othersSubCats)
            {
                existingSubCombo.Items.Add(new ComboBoxItem 
                { 
                    Content = $"{s.Name} (from {s.Category?.Name ?? "Others"})", 
                    Tag = s 
                });
            }
            Grid.SetColumn(existingSubCombo, 0);
            assignExistingRow.Children.Add(existingSubCombo);

            var assignBtn = new Button
            {
                Content = "＋ Assign", FontSize = 12, Height = 32, Padding = new Thickness(14, 0, 14, 0),
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xf1)), // Indigo
                Tag = existingSubCombo, ToolTip = "Keep in old category, just enable in this one too"
            };
            assignBtn.Click += AssignSubCatToBoth_Click;
            Grid.SetColumn(assignBtn, 2);
            assignExistingRow.Children.Add(assignBtn);

            assignExistingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            assignExistingRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var moveBtn = new Button
            {
                Content = "⇄ Move", FontSize = 12, Height = 32, Padding = new Thickness(14, 0, 14, 0),
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b)), // Slate
                Tag = existingSubCombo, ToolTip = "Completely move from old category to this one"
            };
            moveBtn.Click += MoveSubCat_Click;
            Grid.SetColumn(moveBtn, 4);
            assignExistingRow.Children.Add(moveBtn);

            mainStack.Children.Add(assignExistingRow);
        }

        return new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20, 16, 20, 16), Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xe2, 0xe8, 0xf0)),
            BorderThickness = new Thickness(1), Child = mainStack
        };
    }

    private void EditCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not object[] data || data.Length < 2) return;
        if (data[1] is Border editPanel)
        {
            editPanel.Visibility = editPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private async void SaveEditCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not object[] data || data.Length < 4) return;
        if (data[0] is not int catId) return;

        var nameBox = data[1] as TextBox;
        var iconBox = data[2] as TextBox;
        var colorBox = data[3] as TextBox;

        var newName = nameBox?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(newName))
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = "Category name cannot be empty.";
            return;
        }

        try
        {
            var updated = new Category
            {
                Id = catId,
                Name = newName,
                Icon = iconBox?.Text.Trim() ?? "🏷",
                Color = colorBox?.Text.Trim() ?? "#667eea"
            };
            await App.CategoryService!.UpdateAsync(updated);
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Category \"{newName}\" updated!";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void CreateCat_Click(object sender, RoutedEventArgs e)
    {
        var name = NewCatName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = "Category name is required.";
            return;
        }

        try
        {
            var cat = new Category
            {
                Name = name,
                Icon = NewCatIcon.Text.Trim(),
                Color = NewCatColor.Text.Trim()
            };
            await App.CategoryService!.CreateAsync(cat);
            NewCatName.Text = "";
            NewCatIcon.Text = "🏷";
            NewCatColor.Text = "#667eea";
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Category \"{name}\" created!";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void DeleteCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Category catToDelete) return;

        try
        {
            // Get item counts for this category
            var (invCount, menuCount) = await App.CategoryService!.GetItemCountsForCategoryAsync(catToDelete.Name);
            var totalItems = invCount + menuCount;

            if (totalItems == 0)
            {
                // No items — simple confirmation
                bool confirmed = await App.Notifications!.ShowConfirmAsync(
                    "Confirm Delete",
                    $"Delete category \"{catToDelete.Name}\" and all department assignments?\n\nThis category has no items.");
                if (!confirmed) return;

                await App.CategoryService!.DeleteAsync(catToDelete.Id);
                CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
                CatStatusMessage.Text = "✓ Category deleted.";
                LoadCategories();
                return;
            }

            // Has items — show advanced dialog
            var allCategories = await App.CategoryService!.GetAllAsync();
            var otherCategories = allCategories.Where(c => c.Id != catToDelete.Id).ToList();

            var dialog = new Views.Dialogs.CategoryDeleteDialog(
                catToDelete.Name, invCount, menuCount, otherCategories);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() != true || dialog.SelectedAction == null) return;

            switch (dialog.SelectedAction)
            {
                case "move_existing":
                    await App.CategoryService!.MoveItemsToCategoryAsync(catToDelete.Name, dialog.TargetCategoryName!);
                    break;

                case "move_new":
                    // Create the new category first
                    var newCat = new Category
                    {
                        Name = dialog.TargetCategoryName!,
                        Icon = "🏷",
                        Color = "#667eea"
                    };
                    await App.CategoryService!.CreateAsync(newCat);
                    await App.CategoryService!.MoveItemsToCategoryAsync(catToDelete.Name, dialog.TargetCategoryName!);
                    break;

                case "delete_all":
                    await App.CategoryService!.DeleteItemsInCategoryAsync(catToDelete.Name);
                    break;
            }

            // Now delete the category itself
            await App.CategoryService!.DeleteAsync(catToDelete.Id);
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Category \"{catToDelete.Name}\" deleted.";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void AssignDeptToCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ComboBox combo) return;
        if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not int deptId) return;
        if (combo.Tag is not int catId) return;

        try
        {
            await App.CategoryDepartmentService!.AssignCategoryAsync(deptId, catId);
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void RemoveDeptFromCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int[] ids || ids.Length < 2) return;

        try
        {
            await App.CategoryDepartmentService!.RemoveCategoryAsync(ids[1], ids[0]);
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void CreateSubCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not object[] data || data.Length < 2) return;
        if (data[0] is not int catId || data[1] is not TextBox subBox) return;

        var name = subBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == "Add new sub-category...")
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = "Sub-category name is required.";
            return;
        }

        try
        {
            await App.SubCategoryService!.CreateAsync(new SubCategory { Name = name, CategoryId = catId });
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Sub-category \"{name}\" added!";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void EditSubCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SubCategory sc) return;

        var newName = await App.Notifications!.ShowInputAsync("Rename Sub-category", $"Enter new name for \"{sc.Name}\":", sc.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == sc.Name) return;

        try
        {
            // Use a temporary object for the update to avoid modifying the tracked object 
            // before the service call (which helps correctly identify the old name)
            var updatedSc = new SubCategory 
            { 
                Id = sc.Id, 
                Name = newName.Trim(), 
                CategoryId = sc.CategoryId 
            };

            await App.SubCategoryService!.UpdateAsync(updatedSc);
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Sub-category renamed to \"{updatedSc.Name}\"";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void DeleteSubCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int subId) return;

        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Delete",
            "Delete this sub-category? All items using this sub-category name will have their sub-category field cleared.");
        if (!confirmed) return;

        try
        {
            await App.SubCategoryService!.DeleteAsync(subId);
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = "✓ Sub-category deleted.";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    // ─── SAVE (branding) ───

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveBtn.IsEnabled = false;
            StatusMessage.Text = "Saving...";

            var settings = new BrandingSettings
            {
                LogoEmoji = LogoEmojiBox.Text.Trim(),
                LogoImagePath = _settings?.LogoImagePath,
                BusinessNameLine1 = NameLine1Box.Text.Trim(),
                BusinessNameLine2 = NameLine2Box.Text.Trim(),
                Tagline = TaglineBox.Text.Trim(),
                Description = DescriptionBox.Text.Trim(),
                CopyrightText = CopyrightBox.Text.Trim(),
                WindowTitle = WindowTitleBox.Text.Trim(),
                WelcomeTitle = WelcomeTitleBox.Text.Trim(),
                WelcomeSubtitle = WelcomeSubtitleBox.Text.Trim(),
                CurrencySymbol = CurrencySymbolBox.Text.Trim(),
                CurrencyCode = CurrencyCodeBox.Text.Trim(),
                CurrencyPosition = (CurrencyPositionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "before",
                ThousandsSeparator = (ThousandsSepBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ",",
                DecimalSeparator = (DecimalSepBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ".",
                DecimalPlaces = int.TryParse((DecimalPlacesBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var dp) ? dp : 2,
                Card1Color1 = Card1Color1Box.Text.Trim(),
                Card1Color2 = Card1Color2Box.Text.Trim(),
                Card2Color1 = Card2Color1Box.Text.Trim(),
                Card2Color2 = Card2Color2Box.Text.Trim(),
                Card3Color1 = Card3Color1Box.Text.Trim(),
                Card3Color2 = Card3Color2Box.Text.Trim(),
                Card4Color1 = Card4Color1Box.Text.Trim(),
                Card4Color2 = Card4Color2Box.Text.Trim(),
                CardTextColor = CardTextColorBox.Text.Trim(),
                ReceiptHeader = ReceiptHeaderBox.Text.Trim(),
                ReceiptFooter = ReceiptFooterBox.Text.Trim(),
                PrintLogoOnReceipt = PrintLogoOnReceiptToggle.IsChecked == true,
                ShowDemoLogin = ShowDemoLoginToggle.IsChecked == true,
                TrademarkText = TrademarkBox.Text.Trim(),
                LoginButtonText = LoginButtonTextBox.Text.Trim(),
                EmailLabel = EmailLabelBox.Text.Trim(),
                PasswordLabel = PasswordLabelBox.Text.Trim(),
                QuickLoginTitle = QuickLoginTitleBox.Text.Trim(),
                PasswordHintText = PasswordHintBox.Text.Trim(),
                FeaturePill1 = FeaturePill1Box.Text.Trim(),
                FeaturePill2 = FeaturePill2Box.Text.Trim(),
                FeaturePill3 = FeaturePill3Box.Text.Trim(),
                FeaturePillBgColor = PillBgColorBox.Text.Trim(),
                FeaturePillTextColor = PillTextColorBox.Text.Trim(),
                TitleBarColor = TitleBarColorBox.Text.Trim(),
                TitleBarBackgroundColor = TitleBarBackgroundColorBox.Text.Trim(),
                ProductCardSize = _selectedCardSize,
                CategoryTabSize = _selectedCatTabSize,
                ProductCardFontSize = int.TryParse((CardFontSizeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var fs) ? fs : 0,
                ProductCardFontWeight = (CardFontWeightBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "SemiBold",
                EnabledPaymentMethods = BuildEnabledPaymentMethods(),
                SidebarBgColor = SidebarBgColorBox.Text.Trim(),
                SidebarTextColor = SidebarTextColorBox.Text.Trim(),
                SidebarBgImagePath = _sidebarBgImagePath == "__CLEAR__" ? null : (string.IsNullOrEmpty(_sidebarBgImagePath) ? _settings?.SidebarBgImagePath : _sidebarBgImagePath),
                SidebarNavActiveColor = SidebarNavActiveColorBox.Text.Trim(),
                WelcomeBgColor = WelcomeBgColorBox.Text.Trim(),
                WelcomeTextColor = WelcomeTextColorBox.Text.Trim(),
                WelcomeBgImagePath = _welcomeBgImagePath == "__CLEAR__" ? null : (string.IsNullOrEmpty(_welcomeBgImagePath) ? _settings?.WelcomeBgImagePath : _welcomeBgImagePath),
                UpdateCheckUrl = UpdateCheckUrlBox.Text.Trim()
            };

            _settings = await App.BrandingService!.SaveSettingsAsync(settings);
            StatusMessage.Text = "\u2713 Settings saved! Theme colors updated.";
            
            // Refresh the main window colors immediately
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.RefreshThemeColors();
            }
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SaveBtn.IsEnabled = true;
        }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));
            UpdateStatusMessage.Text = "Checking for updates...";
            
            // Use the URL from the textbox to allow testing before saving
            var currentUrl = UpdateCheckUrlBox.Text.Trim();
            var updateInfo = await App.UpdateService!.CheckForUpdateAsync(currentUrl);
            
            if (updateInfo != null)
            {
                UpdateStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
                UpdateStatusMessage.Text = $"New version available: {updateInfo.LatestVersion}";
                
                // Also trigger the banner in MainWindow if it's open
                if (Window.GetWindow(this) is MainWindow mw)
                {
                    mw.ShowUpdateBanner(updateInfo);
                }
            }
            else
            {
                UpdateStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8b));
                UpdateStatusMessage.Text = "You are running the latest version.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            UpdateStatusMessage.Text = $"Check failed: {ex.Message}";
        }
    }

    // ─── RESET ───

    private async void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Reset",
            "Reset all branding to defaults?");

        if (!confirmed) return;

        try
        {
            var defaults = new BrandingSettings();
            _settings = await App.BrandingService!.SaveSettingsAsync(defaults);
            _isPopulating = true;
            PopulateFields();
            _isPopulating = false;
            StatusMessage.Text = "\u2713 Reset to defaults. Changes will apply on next login.";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Error: {ex.Message}";
        }
    }
    private async void MoveSubCat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ComboBox combo) return;
        if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not SubCategory sc || combo.Tag is not int targetCatId) return;

        bool confirmed = await App.Notifications!.ShowConfirmAsync(
            "Confirm Move",
            $"Move sub-category \"{sc.Name}\" to this category? All existing inventory and menu items in \"{sc.Name}\" will be updated to the new category.");
        if (!confirmed) return;

        try
        {
            // Use a temporary object for the update to avoid modifying the tracked object 
            // before the service call (which helps correctly identify the old category)
            var updatedSc = new SubCategory
            {
                Id = sc.Id,
                Name = sc.Name,
                CategoryId = targetCatId
            };

            await App.SubCategoryService!.UpdateAsync(updatedSc);
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Sub-category \"{sc.Name}\" moved!";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }

    private async void AssignSubCatToBoth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ComboBox combo) return;
        if (combo.SelectedItem is not ComboBoxItem item || item.Tag is not SubCategory sc || combo.Tag is not int targetCatId) return;

        try
        {
            // Just create a duplicate record in the target category 
            // (Doesn't move items, just makes the category name available there)
            await App.SubCategoryService!.CreateAsync(new SubCategory { Name = sc.Name, CategoryId = targetCatId });
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));
            CatStatusMessage.Text = $"✓ Sub-category \"{sc.Name}\" assigned to both!";
            LoadCategories();
        }
        catch (Exception ex)
        {
            CatStatusMessage.Foreground = new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44));
            CatStatusMessage.Text = $"Error: {ex.Message}";
        }
    }
}
