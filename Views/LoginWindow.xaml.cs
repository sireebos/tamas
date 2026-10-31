
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RestaurantManager.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        App.ApplyLogoIcon(this);
        Loaded += LoginWindow_Loaded;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = await App.BrandingService!.GetSettingsAsync();
            Title = settings.WindowTitle;
            LogoText.Text = settings.LogoEmoji;
            var resolvedLogoPath = AdminSettingsView.ResolveImagePath(settings.LogoImagePath);
            if (resolvedLogoPath != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(resolvedLogoPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    LogoImage.Source = bmp;
                    LogoImage.Visibility = Visibility.Visible;
                    LogoText.Visibility = Visibility.Collapsed;
                }
                catch { /* fall back to emoji logo */ }
            }
            BrandLine1.Text = settings.BusinessNameLine1;
            BrandLine2.Text = settings.BusinessNameLine2;
            TaglineText.Text = settings.Tagline;
            DescriptionText.Text = settings.Description;
            CopyrightText.Text = settings.CopyrightText;
            
            TrademarkFooterRight.Text = settings.TrademarkText;
            WelcomeTitleText.Text = settings.WelcomeTitle;
            WelcomeSubtitleText.Text = settings.WelcomeSubtitle;
            LoginButton.Content = settings.LoginButtonText;
            EmailLabelText.Text = settings.EmailLabel;
            PasswordLabelText.Text = settings.PasswordLabel;
            QuickLoginLabel.Text = settings.QuickLoginTitle;
            DemoPasswordHint.Text = settings.PasswordHintText;
            Pill1Text.Text = settings.FeaturePill1;
            Pill2Text.Text = settings.FeaturePill2;
            Pill3Text.Text = settings.FeaturePill3;

            // Apply pill colors
            try
            {
                var pillBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FeaturePillBgColor));
                var pillFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FeaturePillTextColor));
                Pill1Bg.Background = pillBg; Pill2Bg.Background = pillBg.Clone(); Pill3Bg.Background = pillBg.Clone();
                Pill1Text.Foreground = pillFg; Pill2Text.Foreground = pillFg.Clone(); Pill3Text.Foreground = pillFg.Clone();
            }
            catch { /* keep defaults */ }

            // Show/hide quick login accounts based on setting
            if (settings.ShowDemoLogin)
            {
                await LoadDemoAccounts();
                // LoadDemoAccounts will hide the section if no active users exist
            }
            else
            {
                DemoAccountsSection.Visibility = Visibility.Collapsed;
            }

            // Apply sidebar background image to left panel
            var resolvedSidebarPath = AdminSettingsView.ResolveImagePath(settings.SidebarBgImagePath);
            if (resolvedSidebarPath != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(resolvedSidebarPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    LeftPanel.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, Opacity = 0.9 };
                }
                catch { }
            }

            // Apply welcome background image to right panel
            var resolvedWelcomePath = AdminSettingsView.ResolveImagePath(settings.WelcomeBgImagePath);
            if (resolvedWelcomePath != null)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(resolvedWelcomePath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    RightPanel.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill, Opacity = 0.85 };
                }
                catch { }
            }
        }
        catch
        {
            // Use defaults from XAML if settings can't load
        }
    }

    private async Task LoadDemoAccounts()
    {
        try
        {
            var users = await App.UserService!.GetAllUsersAsync();
            DemoAccountsList.Children.Clear();

            if (users == null || users.Count == 0)
            {
                DemoAccountsSection.Visibility = Visibility.Collapsed;
                return;
            }

            string GetRoleEmoji(string? role) => role?.ToLower() switch
            {
                "developer" => "🔧",
                "admin" => "👤",
                "staff" => "👨‍🍳",
                "cashier" => "💰",
                _ => "👤"
            };

            string GetRoleLabel(Models.User user)
            {
                var role = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(user.Role ?? "Staff");
                if (!string.IsNullOrEmpty(user.Department))
                    return $"{role} • {System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(user.Department)}";
                return role;
            }

            foreach (var user in users.Where(u => u.Status == "active"))
            {
                var btn = new Button
                {
                    Tag = user.Email,
                    Style = (Style)FindResource("DemoAccountBtn")
                };
                btn.Click += DemoAccount_Click;

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = GetRoleEmoji(user.Role),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var info = new StackPanel();
                info.Children.Add(new TextBlock
                {
                    Text = $"{user.Name}  ({user.Email})",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#b0b0d0"))
                });
                info.Children.Add(new TextBlock
                {
                    Text = GetRoleLabel(user),
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5a5a7a"))
                });
                sp.Children.Add(info);
                btn.Content = sp;

                DemoAccountsList.Children.Add(btn);
            }

            // Only show section if buttons were actually added
            DemoAccountsSection.Visibility = DemoAccountsList.Children.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            App.Logger?.LogWarning(ex, "Failed to load demo accounts");
            DemoAccountsSection.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DemoAccount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string email)
        {
            EmailBox.Text = email;
            PasswordBox.Password = "password";
            ErrorMessage.Text = "";
        }
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        var email = EmailBox.Text;
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ErrorMessage.Text = "Please enter email and password";
            return;
        }

        try
        {
            LoginButton.IsEnabled = false;
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorMessage.Text = "";

            var user = await App.UserService!.AuthenticateAsync(email, password);
            if (user != null)
            {
                App.CurrentUser = user;
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorMessage.Text = "Invalid email or password";
                PasswordBox.Clear();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }
}
