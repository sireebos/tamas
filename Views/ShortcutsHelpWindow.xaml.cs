using System.Windows;

namespace RestaurantManager.Views;

public partial class ShortcutsHelpWindow : Window
{
    public ShortcutsHelpWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
