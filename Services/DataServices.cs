using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using RestaurantManager.Models;
using RestaurantManager.Data;
using Microsoft.EntityFrameworkCore;

namespace RestaurantManager.Services;

public interface IOrderService
{
    Task<List<Order>> GetAllOrdersAsync();
    Task<Order?> GetOrderAsync(int id);
    Task<Order> CreateOrderAsync(Order order);
    Task<Order> UpdateOrderAsync(Order order);
    Task DeleteOrderAsync(int id);
}

public class OrderService : IOrderService
{
    private readonly RestaurantDbContext _dbContext;

    public OrderService(RestaurantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Log for sync
        _dbContext.SyncLogs.Add(new SyncLog
        {
            EntityType = "orders",
            EntityId = order.Id,
            Action = "create",
            Data = System.Text.Json.JsonSerializer.Serialize(order)
        });
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return order;
    }

    public async Task<Order> UpdateOrderAsync(Order order)
    {
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        order.UpdatedAt = DateTime.Now;
        _dbContext.Orders.Update(order);

        _dbContext.SyncLogs.Add(new SyncLog
        {
            EntityType = "orders",
            EntityId = order.Id,
            Action = "update",
            Data = System.Text.Json.JsonSerializer.Serialize(order)
        });
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return order;
    }

    public async Task DeleteOrderAsync(int id)
    {
        var order = await _dbContext.Orders.FindAsync(id);
        if (order != null)
        {
            using var tx = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.Orders.Remove(order);

            _dbContext.SyncLogs.Add(new SyncLog
            {
                EntityType = "orders",
                EntityId = id,
                Action = "delete"
            });
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }
}

public interface IInventoryService
{
    Task<List<InventoryItem>> GetAllItemsAsync();
    Task<InventoryItem?> GetItemAsync(int id);
    Task<InventoryItem> CreateItemAsync(InventoryItem item);
    Task<InventoryItem> UpdateItemAsync(InventoryItem item);
    Task<InventoryItem> UpdateQuantityAsync(int itemId, int quantity);
    Task DeleteItemAsync(int id);
    Task<List<InventoryItem>> GetLowStockItemsAsync();
    Task<List<string>> GetCategoriesAsync();
}

public class InventoryService : IInventoryService
{
    private readonly RestaurantDbContext _dbContext;

    public InventoryService(RestaurantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<InventoryItem>> GetAllItemsAsync()
    {
        return await _dbContext.InventoryItems
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<InventoryItem?> GetItemAsync(int id)
    {
        return await _dbContext.InventoryItems.FindAsync(id);
    }

    public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
    {
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        _dbContext.InventoryItems.Add(item);
        await _dbContext.SaveChangesAsync();
        _dbContext.SyncLogs.Add(new SyncLog
        {
            EntityType = "inventory_items",
            EntityId = item.Id,
            Action = "create",
            Data = System.Text.Json.JsonSerializer.Serialize(item)
        });
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();
        return item;
    }

    public async Task<InventoryItem> UpdateItemAsync(InventoryItem item)
    {
        var existing = await _dbContext.InventoryItems.FindAsync(item.Id);
        if (existing == null) throw new InvalidOperationException("Item not found");
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        existing.Name = item.Name;
        existing.Description = item.Description;
        existing.Category = item.Category;
        existing.Unit = item.Unit;
        existing.CostPrice = item.CostPrice;
        existing.MinLevel = item.MinLevel;
        existing.ReorderQuantity = item.ReorderQuantity;
        existing.Quantity = item.Quantity;
        existing.UpdatedAt = DateTime.Now;
        _dbContext.SyncLogs.Add(new SyncLog
        {
            EntityType = "inventory_items",
            EntityId = item.Id,
            Action = "update",
            Data = System.Text.Json.JsonSerializer.Serialize(existing)
        });
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();
        return existing;
    }

    public async Task DeleteItemAsync(int id)
    {
        var item = await _dbContext.InventoryItems.FindAsync(id);
        if (item != null)
        {
            using var tx = await _dbContext.Database.BeginTransactionAsync();
            _dbContext.InventoryItems.Remove(item);
            _dbContext.SyncLogs.Add(new SyncLog
            {
                EntityType = "inventory_items",
                EntityId = id,
                Action = "delete"
            });
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await _dbContext.InventoryItems
            .Where(i => i.Category != null)
            .Select(i => i.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<InventoryItem> UpdateQuantityAsync(int itemId, int quantity)
    {
        var item = await _dbContext.InventoryItems.FindAsync(itemId);
        if (item == null) throw new InvalidOperationException("Item not found");

        using var tx = await _dbContext.Database.BeginTransactionAsync();
        var oldQuantity = item.Quantity;
        item.Quantity = quantity;
        item.UpdatedAt = DateTime.Now;

        _dbContext.InventoryItems.Update(item);

        // Log the change
        _dbContext.InventoryLogs.Add(new InventoryLog
        {
            ItemId = itemId,
            QuantityChange = quantity - oldQuantity,
            Reason = "Manual adjustment"
        });

        _dbContext.SyncLogs.Add(new SyncLog
        {
            EntityType = "inventory_items",
            EntityId = itemId,
            Action = "update",
            Data = System.Text.Json.JsonSerializer.Serialize(item)
        });
        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return item;
    }

    public async Task<List<InventoryItem>> GetLowStockItemsAsync()
    {
        return await _dbContext.InventoryItems
            .Where(i => i.Quantity < i.MinLevel)
            .OrderBy(i => i.Quantity)
            .ToListAsync();
    }
}

public interface IUserService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> AuthenticateAsync(string email, string password);
    Task<List<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(User user);
    Task DeleteUserAsync(int id);
}

public class UserService : IUserService
{
    private readonly RestaurantDbContext _dbContext;

    public UserService(RestaurantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var user = await GetUserByEmailAsync(email);
        if (user == null) return null;

        // Secure password verification using BCrypt
        try
        {
            if (BCrypt.Net.BCrypt.Verify(password, user.Password))
                return user;
        }
        catch
        {
            // Fallback: if stored password is not a BCrypt hash (legacy),
            // verify plain and auto-upgrade to BCrypt
            if (user.Password == password)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
                user.UpdatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
                return user;
            }
        }

        return null;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _dbContext.Users.OrderBy(u => u.Name).ToListAsync();
    }

    public async Task<User> CreateUserAsync(User user)
    {
        // Hash password before storing
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _dbContext.Users.FindAsync(id);
        if (user != null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
        }
    }
}

public interface ICategoryService
{
    Task<List<Category>> GetAllAsync();
    Task<Category> CreateAsync(Category category);
    Task<Category> UpdateAsync(Category category);
    Task DeleteAsync(int id);
    Task SyncMissingCategoriesAsync();
    Task<(int inventoryCount, int menuCount)> GetItemCountsForCategoryAsync(string categoryName);
    Task MoveItemsToCategoryAsync(string fromCategoryName, string toCategoryName);
    Task DeleteItemsInCategoryAsync(string categoryName);
}

public class CategoryService : ICategoryService
{
    private readonly RestaurantDbContext _dbContext;
    public CategoryService(RestaurantDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<Category>> GetAllAsync()
        => await _dbContext.Categories.OrderBy(c => c.Name).ToListAsync();

    public async Task SyncMissingCategoriesAsync()
    {
        // Get all unique categories from items
        var invCategories = await _dbContext.InventoryItems
            .Where(i => !string.IsNullOrEmpty(i.Category))
            .Select(i => i.Category!)
            .Distinct()
            .ToListAsync();

        var menuCategories = await _dbContext.MenuItems
            .Where(i => !string.IsNullOrEmpty(i.Category))
            .Select(i => i.Category!)
            .Distinct()
            .ToListAsync();

        var allItemCategories = invCategories.Concat(menuCategories).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Get existing formal categories
        var existingCategories = await _dbContext.Categories.ToListAsync();
        var existingCatNames = existingCategories.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add missing categories
        foreach (var catName in allItemCategories)
        {
            if (!existingCatNames.Contains(catName))
            {
                var newCat = new Category { Name = catName, Icon = "🏷", Color = "#667eea" };
                _dbContext.Categories.Add(newCat);
                existingCategories.Add(newCat);
                existingCatNames.Add(catName);
            }
        }
        await _dbContext.SaveChangesAsync();

        // Now handle sub-categories
        var invSubCats = await _dbContext.InventoryItems
            .Where(i => !string.IsNullOrEmpty(i.Category) && !string.IsNullOrEmpty(i.SubCategory))
            .Select(i => new { i.Category, i.SubCategory })
            .Distinct()
            .ToListAsync();

        var menuSubCats = await _dbContext.MenuItems
            .Where(i => !string.IsNullOrEmpty(i.Category) && !string.IsNullOrEmpty(i.SubCategory))
            .Select(i => new { i.Category, i.SubCategory })
            .Distinct()
            .ToListAsync();

        var allItemSubCats = invSubCats.Concat(menuSubCats)
            .GroupBy(x => new { Cat = x.Category!.ToLower(), Sub = x.SubCategory!.ToLower() })
            .Select(g => g.First())
            .ToList();

        var existingSubCats = await _dbContext.SubCategories.Include(s => s.Category).ToListAsync();

        foreach (var itemSub in allItemSubCats)
        {
            var parentCat = existingCategories.FirstOrDefault(c => string.Equals(c.Name, itemSub.Category, StringComparison.OrdinalIgnoreCase));
            if (parentCat != null)
            {
                var exists = existingSubCats.Any(s => s.CategoryId == parentCat.Id && string.Equals(s.Name, itemSub.SubCategory, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    var newSub = new SubCategory { Name = itemSub.SubCategory!, CategoryId = parentCat.Id };
                    _dbContext.SubCategories.Add(newSub);
                    existingSubCats.Add(newSub);
                }
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<Category> CreateAsync(Category category)
    {
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateAsync(Category category)
    {
        var existing = await _dbContext.Categories.FindAsync(category.Id);
        if (existing == null) throw new InvalidOperationException("Category not found");

        var entry = _dbContext.Entry(existing);
        var oldName = (string)entry.Property(x => x.Name).OriginalValue;
        
        existing.Name = category.Name;
        existing.Icon = category.Icon;
        existing.Color = category.Color;

        // If the name changed, cascade to all items that reference this category by name
        if (!string.Equals(oldName, category.Name, StringComparison.Ordinal) && !string.IsNullOrEmpty(oldName))
        {
            // Update InventoryItems
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE InventoryItems SET Category = {0} WHERE Category = {1}",
                category.Name, oldName);

            // Update MenuItems
            await _dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE MenuItems SET Category = {0} WHERE Category = {1}",
                category.Name, oldName);
        }

        await _dbContext.SaveChangesAsync();
        
        // Clear tracker so UI refreshes items properly if names cascaded
        if (!string.Equals(oldName, category.Name, StringComparison.Ordinal) && !string.IsNullOrEmpty(oldName))
        {
            _dbContext.ChangeTracker.Clear();
        }

        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var cat = await _dbContext.Categories.FindAsync(id);
        if (cat == null) return;
        
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Categories.Remove(cat);
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();

            // Clear tracker so UI refreshes items properly
            _dbContext.ChangeTracker.Clear();
        }
        catch { await tx.RollbackAsync(); throw; }
    }

    public async Task<(int inventoryCount, int menuCount)> GetItemCountsForCategoryAsync(string categoryName)
    {
        var invCount = await _dbContext.InventoryItems
            .CountAsync(i => i.Category != null && i.Category.ToLower() == categoryName.ToLower());
        var menuCount = await _dbContext.MenuItems
            .CountAsync(m => m.Category != null && m.Category.ToLower() == categoryName.ToLower());
        return (invCount, menuCount);
    }

    public async Task MoveItemsToCategoryAsync(string fromCategoryName, string toCategoryName)
    {
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE InventoryItems SET Category = {0} WHERE LOWER(Category) = LOWER({1})",
            toCategoryName, fromCategoryName);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE MenuItems SET Category = {0} WHERE LOWER(Category) = LOWER({1})",
            toCategoryName, fromCategoryName);
        _dbContext.ChangeTracker.Clear();
    }

    public async Task DeleteItemsInCategoryAsync(string categoryName)
    {
        var invItems = await _dbContext.InventoryItems
            .Where(i => i.Category != null && i.Category.ToLower() == categoryName.ToLower())
            .ToListAsync();
        _dbContext.InventoryItems.RemoveRange(invItems);

        var menuItems = await _dbContext.MenuItems
            .Where(m => m.Category != null && m.Category.ToLower() == categoryName.ToLower())
            .ToListAsync();
        _dbContext.MenuItems.RemoveRange(menuItems);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }
}

public interface IBrandingService
{
    Task<BrandingSettings> GetSettingsAsync();
    Task<BrandingSettings> SaveSettingsAsync(BrandingSettings settings);
}

public interface IDepartmentService
{
    Task<List<Department>> GetAllAsync();
    Task<Department> CreateAsync(Department department);
    Task<Department> UpdateAsync(Department department);
    Task DeleteAsync(int id);
    Task<List<UserDepartment>> GetUserDepartmentsAsync(int departmentId);
    Task AssignUserAsync(int departmentId, int userId);
    Task RemoveUserAsync(int departmentId, int userId);
    Task<List<Department>> GetUserDepartmentsForUserAsync(int userId);
}

public class DepartmentService : IDepartmentService
{
    private readonly RestaurantDbContext _dbContext;
    public DepartmentService(RestaurantDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<Department>> GetAllAsync()
        => await _dbContext.Departments.OrderBy(d => d.Name).ToListAsync();

    public async Task<Department> CreateAsync(Department department)
    {
        _dbContext.Departments.Add(department);
        await _dbContext.SaveChangesAsync();
        return department;
    }

    public async Task<Department> UpdateAsync(Department department)
    {
        var existing = await _dbContext.Departments.FindAsync(department.Id);
        if (existing == null) throw new InvalidOperationException("Department not found");
        existing.Name = department.Name;
        existing.Icon = department.Icon;
        existing.Color = department.Color;
        await _dbContext.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var dept = await _dbContext.Departments.FindAsync(id);
        if (dept == null) return;
        _dbContext.Departments.Remove(dept);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<UserDepartment>> GetUserDepartmentsAsync(int departmentId)
        => await _dbContext.UserDepartments
            .Include(ud => ud.User)
            .Where(ud => ud.DepartmentId == departmentId)
            .ToListAsync();

    public async Task AssignUserAsync(int departmentId, int userId)
    {
        var exists = await _dbContext.UserDepartments
            .AnyAsync(ud => ud.DepartmentId == departmentId && ud.UserId == userId);
        if (exists) return;
        _dbContext.UserDepartments.Add(new UserDepartment { DepartmentId = departmentId, UserId = userId });
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveUserAsync(int departmentId, int userId)
    {
        var ud = await _dbContext.UserDepartments
            .FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.UserId == userId);
        if (ud == null) return;
        _dbContext.UserDepartments.Remove(ud);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<Department>> GetUserDepartmentsForUserAsync(int userId)
        => await _dbContext.UserDepartments
            .Include(ud => ud.Department)
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.Department!)
            .ToListAsync();
}

public class BrandingService : IBrandingService
{
    private readonly RestaurantDbContext _dbContext;

    public BrandingService(RestaurantDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BrandingSettings> GetSettingsAsync()
    {
        var settings = await _dbContext.BrandingSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new BrandingSettings();
            _dbContext.BrandingSettings.Add(settings);
            await _dbContext.SaveChangesAsync();
        }
        return settings;
    }

    public async Task<BrandingSettings> SaveSettingsAsync(BrandingSettings settings)
    {
        settings.UpdatedAt = DateTime.Now;
        var existing = await _dbContext.BrandingSettings.FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.LogoEmoji = settings.LogoEmoji;
            existing.LogoImagePath = settings.LogoImagePath;
            existing.BusinessNameLine1 = settings.BusinessNameLine1;
            existing.BusinessNameLine2 = settings.BusinessNameLine2;
            existing.Tagline = settings.Tagline;
            existing.Description = settings.Description;
            existing.CopyrightText = settings.CopyrightText;
            existing.WelcomeTitle = settings.WelcomeTitle;
            existing.WelcomeSubtitle = settings.WelcomeSubtitle;
            existing.WindowTitle = settings.WindowTitle;
            existing.CurrencySymbol = settings.CurrencySymbol;
            existing.CurrencyCode = settings.CurrencyCode;
            existing.CurrencyPosition = settings.CurrencyPosition;
            existing.ThousandsSeparator = settings.ThousandsSeparator;
            existing.DecimalSeparator = settings.DecimalSeparator;
            existing.DecimalPlaces = settings.DecimalPlaces;
            existing.Card1Color1 = settings.Card1Color1;
            existing.Card1Color2 = settings.Card1Color2;
            existing.Card2Color1 = settings.Card2Color1;
            existing.Card2Color2 = settings.Card2Color2;
            existing.Card3Color1 = settings.Card3Color1;
            existing.Card3Color2 = settings.Card3Color2;
            existing.Card4Color1 = settings.Card4Color1;
            existing.Card4Color2 = settings.Card4Color2;
            existing.CardTextColor = settings.CardTextColor;
            existing.ReceiptHeader = settings.ReceiptHeader;
            existing.ReceiptFooter = settings.ReceiptFooter;
            existing.PrintLogoOnReceipt = settings.PrintLogoOnReceipt;
            existing.ShowDemoLogin = settings.ShowDemoLogin;
            existing.TrademarkText = settings.TrademarkText;
            existing.TitleBarColor = settings.TitleBarColor;
            existing.TitleBarBackgroundColor = settings.TitleBarBackgroundColor;
            existing.ProductCardSize = settings.ProductCardSize;
            existing.ProductCardFontSize = settings.ProductCardFontSize;
            existing.ProductCardFontWeight = settings.ProductCardFontWeight;
            existing.CategoryTabSize = settings.CategoryTabSize;
            existing.EnabledPaymentMethods = settings.EnabledPaymentMethods;
            existing.SidebarBgColor = settings.SidebarBgColor;
            existing.SidebarTextColor = settings.SidebarTextColor;
            existing.SidebarBgImagePath = settings.SidebarBgImagePath;
            existing.SidebarNavActiveColor = settings.SidebarNavActiveColor;
            existing.WelcomeBgColor = settings.WelcomeBgColor;
            existing.WelcomeTextColor = settings.WelcomeTextColor;
            existing.WelcomeBgImagePath = settings.WelcomeBgImagePath;
            existing.LoginButtonText = settings.LoginButtonText;
            existing.EmailLabel = settings.EmailLabel;
            existing.PasswordLabel = settings.PasswordLabel;
            existing.QuickLoginTitle = settings.QuickLoginTitle;
            existing.PasswordHintText = settings.PasswordHintText;
            existing.FeaturePill1 = settings.FeaturePill1;
            existing.FeaturePill2 = settings.FeaturePill2;
            existing.FeaturePill3 = settings.FeaturePill3;
            existing.FeaturePillBgColor = settings.FeaturePillBgColor;
            existing.FeaturePillTextColor = settings.FeaturePillTextColor;
            existing.UpdateCheckUrl = settings.UpdateCheckUrl;
            existing.UpdatedAt = settings.UpdatedAt;
            _dbContext.BrandingSettings.Update(existing);
        }
        else
        {
            _dbContext.BrandingSettings.Add(settings);
        }
        await _dbContext.SaveChangesAsync();
        return existing ?? settings;
    }
}

public interface ICategoryDepartmentService
{
    Task<List<CategoryDepartment>> GetAllAsync();
    Task<CategoryDepartment> CreateAsync(CategoryDepartment cd);
    Task DeleteAsync(int id);
    Task<List<CategoryDepartment>> GetDepartmentsForCategoryAsync(int categoryId);
    Task<List<CategoryDepartment>> GetCategoriesForDepartmentAsync(int departmentId);
    Task AssignCategoryAsync(int departmentId, int categoryId);
    Task RemoveCategoryAsync(int departmentId, int categoryId);
}

public class CategoryDepartmentService : ICategoryDepartmentService
{
    private readonly RestaurantDbContext _dbContext;
    public CategoryDepartmentService(RestaurantDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<CategoryDepartment>> GetAllAsync()
        => await _dbContext.CategoryDepartments.Include(cd => cd.Category).Include(cd => cd.Department).ToListAsync();

    public async Task<CategoryDepartment> CreateAsync(CategoryDepartment cd)
    {
        _dbContext.CategoryDepartments.Add(cd);
        await _dbContext.SaveChangesAsync();
        return cd;
    }

    public async Task DeleteAsync(int id)
    {
        var cd = await _dbContext.CategoryDepartments.FindAsync(id);
        if (cd == null) return;
        _dbContext.CategoryDepartments.Remove(cd);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<CategoryDepartment>> GetDepartmentsForCategoryAsync(int categoryId)
        => await _dbContext.CategoryDepartments.Include(cd => cd.Department).Where(cd => cd.CategoryId == categoryId).ToListAsync();

    public async Task<List<CategoryDepartment>> GetCategoriesForDepartmentAsync(int departmentId)
        => await _dbContext.CategoryDepartments.Include(cd => cd.Category).Where(cd => cd.DepartmentId == departmentId).ToListAsync();

    public async Task AssignCategoryAsync(int departmentId, int categoryId)
    {
        var exists = await _dbContext.CategoryDepartments.AnyAsync(cd => cd.DepartmentId == departmentId && cd.CategoryId == categoryId);
        if (exists) return;
        _dbContext.CategoryDepartments.Add(new CategoryDepartment { DepartmentId = departmentId, CategoryId = categoryId });
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveCategoryAsync(int departmentId, int categoryId)
    {
        var cd = await _dbContext.CategoryDepartments.FirstOrDefaultAsync(x => x.DepartmentId == departmentId && x.CategoryId == categoryId);
        if (cd == null) return;
        _dbContext.CategoryDepartments.Remove(cd);
        await _dbContext.SaveChangesAsync();
    }
}

// ─── MENU ITEM SERVICE ───

public interface IMenuItemService
{
    Task<List<MenuItem>> GetAllAsync();
    Task<List<MenuItem>> GetAvailableAsync();
    Task<List<string>> GetCategoriesAsync();
    Task<MenuItem?> GetAsync(int id);
    Task<MenuItem> CreateAsync(MenuItem item);
    Task<MenuItem> UpdateAsync(MenuItem item);
    Task DeleteAsync(int id);
}

public class MenuItemService : IMenuItemService
{
    private readonly RestaurantDbContext _dbContext;
    public MenuItemService(RestaurantDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<MenuItem>> GetAllAsync()
        => await _dbContext.MenuItems.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();

    public async Task<List<MenuItem>> GetAvailableAsync()
        => await _dbContext.MenuItems.Where(m => m.IsAvailable).OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();

    public async Task<List<string>> GetCategoriesAsync()
        => await _dbContext.MenuItems.Where(m => m.Category != null).Select(m => m.Category!).Distinct().OrderBy(c => c).ToListAsync();

    public async Task<MenuItem?> GetAsync(int id)
        => await _dbContext.MenuItems.FindAsync(id);

    public async Task<MenuItem> CreateAsync(MenuItem item)
    {
        _dbContext.MenuItems.Add(item);
        await _dbContext.SaveChangesAsync();
        return item;
    }

    public async Task<MenuItem> UpdateAsync(MenuItem item)
    {
        item.UpdatedAt = DateTime.Now;
        _dbContext.MenuItems.Update(item);
        await _dbContext.SaveChangesAsync();
        return item;
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _dbContext.MenuItems.FindAsync(id);
        if (item != null)
        {
            _dbContext.MenuItems.Remove(item);
            await _dbContext.SaveChangesAsync();
        }
    }
}

public interface ISubCategoryService
{
    Task<List<SubCategory>> GetAllAsync();
    Task<List<SubCategory>> GetByCategoryAsync(int categoryId);
    Task<SubCategory> CreateAsync(SubCategory sc);
    Task<SubCategory> UpdateAsync(SubCategory sc);
    Task DeleteAsync(int id);
}

public class SubCategoryService : ISubCategoryService
{
    private readonly RestaurantDbContext _dbContext;
    public SubCategoryService(RestaurantDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<SubCategory>> GetAllAsync()
        => await _dbContext.SubCategories.Include(sc => sc.Category).OrderBy(sc => sc.Name).ToListAsync();

    public async Task<List<SubCategory>> GetByCategoryAsync(int categoryId)
        => await _dbContext.SubCategories.Where(sc => sc.CategoryId == categoryId).OrderBy(sc => sc.Name).ToListAsync();

    public async Task<SubCategory> CreateAsync(SubCategory sc)
    {
        _dbContext.SubCategories.Add(sc);
        await _dbContext.SaveChangesAsync();
        return sc;
    }

    public async Task<SubCategory> UpdateAsync(SubCategory sc)
    {
        var existing = await _dbContext.SubCategories
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == sc.Id);
            
        if (existing == null) throw new InvalidOperationException("Sub-category not found");

        var entry = _dbContext.Entry(existing);
        var oldName = (string)entry.Property(x => x.Name).OriginalValue;
        var oldCatId = (int)entry.Property(x => x.CategoryId).OriginalValue;
        
        // Parent category of this sub-category (before modification)
        var oldParentCatName = existing.Category?.Name;

        existing.Name = sc.Name;
        existing.CategoryId = sc.CategoryId;

        // 1. If category changed, we should update the 'Category' string on items
        if (oldCatId != sc.CategoryId)
        {
            var newCat = await _dbContext.Categories.FindAsync(sc.CategoryId);
            
            if (newCat != null && !string.IsNullOrEmpty(oldParentCatName))
            {
                // Update items that match BOTH old sub-category name AND old parent category name
                // (Note: we use 'oldName' in case the name was also changed in the same call)
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE InventoryItems SET Category = {0} WHERE SubCategory = {1} AND Category = {2}",
                    newCat.Name, oldName, oldParentCatName);
                    
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE MenuItems SET Category = {0} WHERE SubCategory = {1} AND Category = {2}",
                    newCat.Name, oldName, oldParentCatName);
            }
        }

        // 2. If name changed, rename them on items. 
        // Scope to parent category to avoid renaming sub-categories with same name in other categories.
        if (!string.Equals(oldName, sc.Name, StringComparison.Ordinal) && !string.IsNullOrEmpty(oldName))
        {
            // We use the category BEFORE it was potentially moved (or same if it wasn't moved)
            var currentParentCat = await _dbContext.Categories.FindAsync(existing.CategoryId);
            var parentCatName = currentParentCat?.Name ?? oldParentCatName;

            if (!string.IsNullOrEmpty(parentCatName))
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE InventoryItems SET SubCategory = {0} WHERE SubCategory = {1} AND Category = {2}",
                    sc.Name, oldName, parentCatName);

                await _dbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE MenuItems SET SubCategory = {0} WHERE SubCategory = {1} AND Category = {2}",
                    sc.Name, oldName, parentCatName);
            }
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var sc = await _dbContext.SubCategories.FindAsync(id);
        if (sc == null) return;
        
        using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.SubCategories.Remove(sc);
            await _dbContext.SaveChangesAsync();
            await tx.CommitAsync();

            // Clear tracker so UI refreshes items properly
            _dbContext.ChangeTracker.Clear();
        }
        catch { await tx.RollbackAsync(); throw; }
    }
}
