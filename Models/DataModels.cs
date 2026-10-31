using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
namespace RestaurantManager.Models;

public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Role is required")]
    public string Role { get; set; } = "staff"; // comma-separated: developer, admin, staff

    public string? Department { get; set; } // kitchen, service, bar, housekeeping, front-desk (for staff)

    public bool HasRole(string role) =>
        Role?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
             .Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)) ?? false;

    [Required(ErrorMessage = "Status is required")]
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class Order
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Order number is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Order number must be 1-50 characters")]
    public string OrderNumber { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Table ID must be positive")]
    public int? TableId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "User ID must be positive")]
    public int? UserId { get; set; }
    public User? User { get; set; }

    [Required(ErrorMessage = "Status is required")]
    public string Status { get; set; } = "pending";

    public string? PaymentMethod { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Total must be non-negative")]
    public decimal Total { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsSynced { get; set; } = false;

    [Required(ErrorMessage = "Order must have items")]
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Order ID must be positive")]
    public int OrderId { get; set; }

    public int? InventoryItemId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative")]
    public decimal Price { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public InventoryItem? InventoryItem { get; set; }
}

public class MenuItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Menu item name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Menu item name must be 1-100 characters")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative")]
    public decimal Price { get; set; }

    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string? Category { get; set; }

    [StringLength(50, ErrorMessage = "SubCategory cannot exceed 50 characters")]
    public string? SubCategory { get; set; }

    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class InventoryItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Inventory item name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Item name must be 1-100 characters")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
    public string? Category { get; set; }

    [StringLength(50, ErrorMessage = "SubCategory cannot exceed 50 characters")]
    public string? SubCategory { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be non-negative")]
    public int Quantity { get; set; }

    [StringLength(20, ErrorMessage = "Unit cannot exceed 20 characters")]
    public string? Unit { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Cost price must be non-negative")]
    public decimal CostPrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Min level must be non-negative")]
    public int MinLevel { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Reorder quantity must be non-negative")]
    public int ReorderQuantity { get; set; }

    [Display(Name = "Stock Value")]
    public decimal StockValue => Quantity * CostPrice;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class InventoryLog
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int QuantityChange { get; set; }
    public string? Reason { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🏷";
    public string Color { get; set; } = "#667eea";
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<SubCategory> SubCategories { get; set; } = new();
    public List<CategoryDepartment> CategoryDepartments { get; set; } = new();
}

public class SubCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "🏢";
    public string Color { get; set; } = "#667eea";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<UserDepartment> UserDepartments { get; set; } = new();
}

public class UserDepartment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int DepartmentId { get; set; }

    public User? User { get; set; }
    public Department? Department { get; set; }
}

public class CategoryDepartment
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int DepartmentId { get; set; }

    public Category? Category { get; set; }
    public Department? Department { get; set; }
}

public class SyncLog
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? Data { get; set; }
    public bool IsSynced { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class BrandingSettings
{
    public int Id { get; set; }
    public string LogoEmoji { get; set; } = "🍽️";
    public string? LogoImagePath { get; set; }
    public string BusinessNameLine1 { get; set; } = "Tama's Palace Spot";
    public string BusinessNameLine2 { get; set; } = "Restaurant";
    public string Tagline { get; set; } = "Restaurant Sales & Management";
    public string Description { get; set; } = "Manage orders, inventory, staff, and sales — all from one dashboard. Built for efficiency.";
    public string CopyrightText { get; set; } = "© 2026 Tama's Palace Spot";
    public string WelcomeTitle { get; set; } = "Welcome back";
    public string WelcomeSubtitle { get; set; } = "Sign in to your account to continue";
    public string WindowTitle { get; set; } = "Tama's Palace Spot - Sales Platform";
    public string CurrencySymbol { get; set; } = "₦";
    public string CurrencyCode { get; set; } = "NGN";
    public string CurrencyPosition { get; set; } = "before"; // before or after
    public string ThousandsSeparator { get; set; } = ",";
    public string DecimalSeparator { get; set; } = ".";
    public int DecimalPlaces { get; set; } = 2;

    // Theme card colors (hex, 4 cards x 2 gradient stops)
    public string Card1Color1 { get; set; } = "#667eea";
    public string Card1Color2 { get; set; } = "#764ba2";
    public string Card2Color1 { get; set; } = "#f97316";
    public string Card2Color2 { get; set; } = "#ea580c";
    public string Card3Color1 { get; set; } = "#10b981";
    public string Card3Color2 { get; set; } = "#059669";
    public string Card4Color1 { get; set; } = "#8b5cf6";
    public string Card4Color2 { get; set; } = "#6d28d9";
    public string CardTextColor { get; set; } = "#ffffff";

    // Receipt customization
    public string ReceiptHeader { get; set; } = "";
    public string ReceiptFooter { get; set; } = "Thank you for your patronage!";
    public bool PrintLogoOnReceipt { get; set; } = true;

    // Developer settings
    public bool ShowDemoLogin { get; set; } = false;
    public string TrademarkText { get; set; } = "POWERED BY TANZAHOST.COM - 08132167507";
    public string TitleBarColor { get; set; } = "#667eea";
    public string TitleBarBackgroundColor { get; set; } = "#ffffff";
    public string UpdateCheckUrl { get; set; } = "https://tanzahost.com/api/restaurant-manager/version.json";

    // Login screen text customization
    public string LoginButtonText { get; set; } = "Sign In";
    public string EmailLabel { get; set; } = "EMAIL";
    public string PasswordLabel { get; set; } = "PASSWORD";
    public string QuickLoginTitle { get; set; } = "QUICK LOGIN";
    public string PasswordHintText { get; set; } = "Password for all: password";
    public string FeaturePill1 { get; set; } = "📋 Orders";
    public string FeaturePill2 { get; set; } = "📦 Inventory";
    public string FeaturePill3 { get; set; } = "📊 Reports";
    public string FeaturePillBgColor { get; set; } = "#ffffff10";
    public string FeaturePillTextColor { get; set; } = "#b0b0d0";

    // Order product card size (small, medium, large, xlarge, xxlarge)
    public string ProductCardSize { get; set; } = "medium";

    // Category tab size (small, medium, large, xlarge)
    public string CategoryTabSize { get; set; } = "medium";
    public int ProductCardFontSize { get; set; } = 0; // 0 = auto (based on card size)
    public string ProductCardFontWeight { get; set; } = "SemiBold"; // Normal, SemiBold, Bold

    // Enabled payment methods (comma-separated: cash,transfer,pos,cash_transfer,cash_pos)
    public string EnabledPaymentMethods { get; set; } = "cash,transfer,pos,cash_transfer,cash_pos";

    // Sidebar customization
    public string SidebarBgColor { get; set; } = "#f8fafc";
    public string SidebarTextColor { get; set; } = "#334155";
    public string? SidebarBgImagePath { get; set; }
    public string SidebarNavActiveColor { get; set; } = "#667eea";

    // Welcome/content area customization
    public string WelcomeBgColor { get; set; } = "#ffffff";
    public string WelcomeTextColor { get; set; } = "#0f172a";
    public string? WelcomeBgImagePath { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string FormatCurrency(decimal amount)
    {
        var formatted = amount.ToString($"N{DecimalPlaces}");
        return CurrencyPosition == "after" ? $"{formatted} {CurrencySymbol}" : $"{CurrencySymbol} {formatted}";
    }
}
