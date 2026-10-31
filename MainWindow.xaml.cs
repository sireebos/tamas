
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RestaurantManager.Views;
using RestaurantManager.Services;

namespace RestaurantManager;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        App.ApplyLogoIcon(this);
        LoadTrademark();
        PreviewKeyDown += HandleKeyboardShortcuts;
        
        if (App.CurrentUser == null)
        {
            ShowLoginWindow();
        }
        else
        {
            UpdateUserInfo();
            UpdateSettingsVisibility();
            if (App.CurrentUser.HasRole("admin") || App.CurrentUser.HasRole("developer"))
                ShowDashboard();
            else
                ShowStaffHome();

            // Check for updates (non-blocking)
            CheckForUpdatesAsync();
        }
    }

    private bool _isClosed;
    private string? _updateDownloadUrl;

    private async void LoadTrademark()
    {
        try
        {
            var settings = await App.BrandingService!.GetSettingsAsync();
            if (_isClosed) return;
            TrademarkFooter.Text = settings.TrademarkText;
            SidebarTrademark.Text = settings.TrademarkText;

            // Update the title bar text from settings
            if (!string.IsNullOrWhiteSpace(settings.WindowTitle))
            {
                AppTitleBar.Title = settings.WindowTitle;
                Title = settings.WindowTitle;
            }

            ApplyThemeColors(settings.TitleBarColor, settings.TitleBarBackgroundColor);
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to load trademark settings"); }
    }

    public async void RefreshThemeColors()
    {
        try
        {
            var settings = await App.BrandingService!.GetSettingsAsync();
            if (_isClosed) return;

            // Refresh title bar name
            if (!string.IsNullOrWhiteSpace(settings.WindowTitle))
            {
                AppTitleBar.Title = settings.WindowTitle;
                Title = settings.WindowTitle;
            }

            // Refresh trademark
            TrademarkFooter.Text = settings.TrademarkText;
            SidebarTrademark.Text = settings.TrademarkText;

            ApplyThemeColors(settings.TitleBarColor, settings.TitleBarBackgroundColor);
            ApplySidebarTheme(settings);
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to refresh theme colors"); }
    }

    private void ApplyThemeColors(string titleBarColorHex, string backgroundColorHex)
    {
        try
        {
            // Apply title bar foreground color (text)
            var titleBarBrush = new System.Windows.Media.BrushConverter().ConvertFromString(titleBarColorHex) as System.Windows.Media.Brush;
            if (titleBarBrush != null && AppTitleBar != null)
            {
                AppTitleBar.Foreground = titleBarBrush;
            }

            // Apply background color to the TITLE BAR only — not the entire window
            var backgroundBrush = new System.Windows.Media.BrushConverter().ConvertFromString(backgroundColorHex) as System.Windows.Media.Brush;
            if (backgroundBrush != null && AppTitleBar != null)
            {
                AppTitleBar.Background = backgroundBrush;
            }
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to apply theme colors"); }
    }

    private void ApplySidebarTheme(RestaurantManager.Models.BrandingSettings settings)
    {
        try
        {
            // Sidebar background color only (images are for login screen)
            try { SidebarPanel.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.SidebarBgColor)); } catch { }
        }
        catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to apply sidebar theme"); }
    }

    private void ShowLoginWindow()
    {
        Hide();
        var loginWindow = new LoginWindow();
        loginWindow.ShowDialog();

        if (_isClosed) return;

        if (App.CurrentUser == null)
        {
            Close();
        }
        else
        {
            Show();
            UpdateUserInfo();
            UpdateSettingsVisibility();
            if (App.CurrentUser!.HasRole("admin") || App.CurrentUser.HasRole("developer"))
                ShowDashboard();
            else
                ShowStaffHome();

            // Check for updates after re-login
            CheckForUpdatesAsync();
        }
    }

    private void UpdateUserInfo()
    {
        var name = App.CurrentUser?.Name ?? "User";
        UserLabel.Text = name;
        var role = App.CurrentUser?.Role ?? "—";
        UserRoleLabel.Text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(role);

        // Extract initials (e.g. "John Doe" → "JD")
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}"
            : name.Length >= 1 ? name[..1] : "U";
        UserInitials.Text = initials.ToUpper();
    }

    private void SetActiveNav(string page)
    {
        DashboardIndicator.Visibility = page == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        OrdersIndicator.Visibility = page == "orders" ? Visibility.Visible : Visibility.Collapsed;
        SalesHistoryIndicator.Visibility = page == "saleshistory" ? Visibility.Visible : Visibility.Collapsed;
        InventoryIndicator.Visibility = page == "inventory" ? Visibility.Visible : Visibility.Collapsed;
        ReportsIndicator.Visibility = page == "reports" ? Visibility.Visible : Visibility.Collapsed;
        SettingsIndicator.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DashboardBtn_Click(object sender, RoutedEventArgs e)
    {
        SidebarPanel.Visibility = Visibility.Visible;
        ShowDashboard();
    }

    private void OrdersBtn_Click(object sender, RoutedEventArgs e)
    {
        var ordersView = new OrdersView();
        ordersView.BackRequested += OrdersView_BackRequested;
        ContentArea.Content = ordersView;
        SidebarPanel.Visibility = Visibility.Collapsed;
        SetActiveNav("orders");
    }

    private void SalesHistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var ordersView = new OrdersView();
        ordersView.BackRequested += OrdersView_BackRequested;
        ordersView.OpenToHistory = true;
        ContentArea.Content = ordersView;
        SidebarPanel.Visibility = Visibility.Collapsed;
        SetActiveNav("saleshistory");
    }

    private void OrdersView_BackRequested()
    {
        SidebarPanel.Visibility = Visibility.Visible;
        if (App.CurrentUser != null && (App.CurrentUser.HasRole("admin") || App.CurrentUser.HasRole("developer")))
            ShowDashboard();
        else
            ShowStaffHome();
    }

    private void InventoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var user = App.CurrentUser;
        if (user != null && (user.HasRole("admin") || user.HasRole("developer")))
        {
            SidebarPanel.Visibility = Visibility.Visible;
            ContentArea.Content = new InventoryView();
            SetActiveNav("inventory");
        }
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        SidebarPanel.Visibility = Visibility.Visible;
        ContentArea.Content = new AdminSettingsView();
        SetActiveNav("settings");
    }

    private void ReportsBtn_Click(object sender, RoutedEventArgs e)
    {
        var user = App.CurrentUser;
        if (user != null && (user.HasRole("admin") || user.HasRole("developer")))
        {
            SidebarPanel.Visibility = Visibility.Visible;
            ContentArea.Content = new ReportsView();
            SetActiveNav("reports");
        }
    }

    private void HelpBtn_Click(object sender, RoutedEventArgs e)
    {
        var help = new ShortcutsHelpWindow();
        help.Owner = this;
        help.ShowDialog();
    }

    private void LogoutBtn_Click(object sender, RoutedEventArgs e)
    {
        App.CurrentUser = null;
        ShowLoginWindow();
    }

    private void UpdateSettingsVisibility()
    {
        var user = App.CurrentUser;
        bool isAdminOrDev = user != null && (user.HasRole("developer") || user.HasRole("admin"));
        SettingsBtn.Visibility = isAdminOrDev ? Visibility.Visible : Visibility.Collapsed;
        InventoryBtn.Visibility = isAdminOrDev ? Visibility.Visible : Visibility.Collapsed;
        ReportsBtn.Visibility = isAdminOrDev ? Visibility.Visible : Visibility.Collapsed;
        DashboardBtn.Visibility = isAdminOrDev ? Visibility.Visible : Visibility.Collapsed;
        InventoryIndicator.Visibility = Visibility.Collapsed;
    }

    private void ShowDashboard()
    {
        ContentArea.Content = new DashboardView();
        UpdateUserInfo();
        SetActiveNav("dashboard");
    }

    private void ShowStaffHome()
    {
        SidebarPanel.Visibility = Visibility.Visible;
        var welcomePanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        welcomePanel.Children.Add(new TextBlock
        {
            Text = $"Welcome, {App.CurrentUser?.Name ?? "User"}!",
            FontSize = 28, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x0f, 0x17, 0x2a)),
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10)
        });
        welcomePanel.Children.Add(new TextBlock
        {
            Text = "Select an option from the sidebar to get started.",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xa3, 0xb8)),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        ContentArea.Content = welcomePanel;
        SetActiveNav("");
    }

    // ─── KEYBOARD SHORTCUTS ───

    private void HandleKeyboardShortcuts(object sender, KeyEventArgs e)
    {
        // Don't intercept if typing in a text field
        if (e.OriginalSource is System.Windows.Controls.TextBox || e.OriginalSource is System.Windows.Controls.PasswordBox)
        {
            // Only allow Escape from text fields
            if (e.Key != Key.Escape) return;
        }

        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var activeView = ContentArea.Content;

        // ─── F12: SHOW HELP ───
        if (e.Key == Key.F12)
        {
            var help = new ShortcutsHelpWindow();
            help.Owner = this;
            help.ShowDialog();
            e.Handled = true;
            return;
        }

        // ─── GLOBAL NAVIGATION (F-keys) ───
        switch (e.Key)
        {
            case Key.F1:
                DashboardBtn_Click(this, new RoutedEventArgs());
                e.Handled = true; return;
            case Key.F2:
                OrdersBtn_Click(this, new RoutedEventArgs());
                e.Handled = true; return;
            case Key.F3:
                SalesHistoryBtn_Click(this, new RoutedEventArgs());
                e.Handled = true; return;
            case Key.F4:
                if (InventoryBtn.Visibility == Visibility.Visible)
                    InventoryBtn_Click(this, new RoutedEventArgs());
                e.Handled = true; return;
            case Key.F5:
                if (ReportsBtn.Visibility == Visibility.Visible)
                    ReportsBtn_Click(this, new RoutedEventArgs());
                e.Handled = true; return;
        }

        // ─── ORDERS VIEW SHORTCUTS ───
        if (activeView is OrdersView ordersView)
        {
            switch (e.Key)
            {
                case Key.P when ctrl:
                    ordersView.ShortcutPlaceOrder();
                    e.Handled = true; return;
                case Key.N when ctrl:
                    ordersView.ShortcutNewTab();
                    e.Handled = true; return;
                case Key.S when ctrl:
                    ordersView.ShortcutSaveDraft();
                    e.Handled = true; return;
                case Key.H when ctrl:
                    ordersView.ShortcutHistory();
                    e.Handled = true; return;
                case Key.T when ctrl:
                    ordersView.ShortcutNewTab();
                    e.Handled = true; return;
                case Key.Delete when ctrl:
                    ordersView.ShortcutClearOrder();
                    e.Handled = true; return;
                case Key.Escape:
                    ordersView.ShortcutEscape();
                    e.Handled = true; return;
                case Key.F when ctrl:
                    ordersView.ShortcutFocusSearch();
                    e.Handled = true; return;
            }
        }

        // ─── DASHBOARD & REPORTS VIEW SHORTCUTS ───
        if (activeView is DashboardView dashboardView)
        {
            if (e.Key == Key.P && ctrl)
            {
                dashboardView.ShortcutPrintEndOfDay();
                e.Handled = true; return;
            }
        }
        if (activeView is ReportsView reportsView)
        {
            if (e.Key == Key.P && ctrl)
            {
                reportsView.ShortcutPrintEndOfDay();
                e.Handled = true; return;
            }
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (App.CurrentUser != null)
        {
            var result = MessageBox.Show("Are you sure you want to exit?", "Confirm Exit",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        if (App.CurrentUser != null)
        {
            App.CurrentUser = null;
        }
        base.OnClosed(e);
    }

    // ─── UPDATE CHECK ───

    public void ShowUpdateBanner(UpdateInfo updateInfo)
    {
        if (updateInfo == null) return;

        _updateDownloadUrl = updateInfo.DownloadUrl;
        var currentVer = App.UpdateService!.GetCurrentVersion();
        UpdateMessage.Text = $"Update available: v{updateInfo.LatestVersion} (current: v{currentVer.Major}.{currentVer.Minor}.{currentVer.Build})";
        UpdateNotes.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) ? "" : updateInfo.ReleaseNotes;
        UpdateDownloadBtn.Visibility = string.IsNullOrWhiteSpace(updateInfo.DownloadUrl)
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;
        UpdateBanner.Visibility = System.Windows.Visibility.Visible;
    }

    private async void CheckForUpdatesAsync()
    {
        try
        {
            if (App.UpdateService == null || App.BrandingService == null) return;

            var settings = await App.BrandingService.GetSettingsAsync();
            var updateInfo = await App.UpdateService.CheckForUpdateAsync(settings.UpdateCheckUrl);

            if (_isClosed) return;

            if (updateInfo != null)
            {
                ShowUpdateBanner(updateInfo);
            }
        }
        catch (Exception ex)
        {
            App.Logger?.LogWarning(ex, "Update check failed silently");
        }
    }

    private void UpdateBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Clicking the banner itself opens the download URL if available
        if (!string.IsNullOrWhiteSpace(_updateDownloadUrl))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _updateDownloadUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to open download URL"); }
        }
    }

    private void UpdateDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_updateDownloadUrl))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _updateDownloadUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { App.Logger?.LogWarning(ex, "Failed to open download URL"); }
        }
    }

    private void UpdateDismiss_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = System.Windows.Visibility.Collapsed;
    }
}
