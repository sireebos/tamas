using System.Windows;

namespace RestaurantManager.Views.Dialogs;

public partial class InputDialog : Window
{
    public string InputValue { get; private set; } = "";

    public InputDialog(string title, string message, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initialValue;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        InputValue = InputBox.Text;
        DialogResult = true;
    }
}
