using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RestaurantManager.Models;

namespace RestaurantManager.Views.Dialogs;

public partial class CategoryDeleteDialog : Window
{
    /// <summary>
    /// Result of the dialog: "move_existing", "move_new", "delete_all", or null if cancelled
    /// </summary>
    public string? SelectedAction { get; private set; }

    /// <summary>
    /// The name of the target category (for move_existing or move_new)
    /// </summary>
    public string? TargetCategoryName { get; private set; }

    public CategoryDeleteDialog(
        string categoryName,
        int inventoryCount,
        int menuCount,
        List<Category> otherCategories)
    {
        InitializeComponent();

        int totalItems = inventoryCount + menuCount;
        HeaderSubtitle.Text = $"Deleting \"{categoryName}\"";

        ItemCountInfo.Text = $"This category contains {totalItems} item{(totalItems != 1 ? "s" : "")}.";

        var breakdown = new List<string>();
        if (inventoryCount > 0) breakdown.Add($"{inventoryCount} inventory item{(inventoryCount != 1 ? "s" : "")}");
        if (menuCount > 0) breakdown.Add($"{menuCount} menu item{(menuCount != 1 ? "s" : "")}");
        ItemBreakdown.Text = string.Join(" • ", breakdown);

        // Populate existing categories combo
        foreach (var cat in otherCategories)
        {
            ExistingCategoryCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{cat.Icon} {cat.Name}",
                Tag = cat.Name
            });
        }

        if (otherCategories.Count == 0)
        {
            OptionMoveExisting.IsEnabled = false;
            OptionMoveExisting.Opacity = 0.5;
        }

        // If there are no items, just show a simple confirm
        if (totalItems == 0)
        {
            ConfirmButton.IsEnabled = true;
            SelectedAction = "no_items";
        }
    }

    private void Option_Checked(object sender, RoutedEventArgs e)
    {
        // Enable/disable controls based on selected option
        ExistingCategoryCombo.IsEnabled = OptionMoveExisting.IsChecked == true;
        NewCategoryNameBox.IsEnabled = OptionMoveNew.IsChecked == true;
        ConfirmButton.IsEnabled = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = null;
        DialogResult = false;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (OptionMoveExisting.IsChecked == true)
        {
            if (ExistingCategoryCombo.SelectedItem is ComboBoxItem item && item.Tag is string targetName)
            {
                SelectedAction = "move_existing";
                TargetCategoryName = targetName;
            }
            else
            {
                MessageBox.Show("Please select a target category.", "⚠ Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        else if (OptionMoveNew.IsChecked == true)
        {
            var newName = NewCategoryNameBox.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Please enter a name for the new category.", "⚠ Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedAction = "move_new";
            TargetCategoryName = newName;
        }
        else if (OptionDeleteAll.IsChecked == true)
        {
            var result = MessageBox.Show(
                "Are you absolutely sure you want to permanently delete ALL items in this category?\n\nThis action CANNOT be undone!",
                "⚠ Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            SelectedAction = "delete_all";
        }
        else if (SelectedAction == "no_items")
        {
            // No items — just delete category
        }
        else
        {
            MessageBox.Show("Please select an action.", "⚠ Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
