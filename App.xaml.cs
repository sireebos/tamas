using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManager.Data;
using RestaurantManager.Services;
using RestaurantManager.Services.Infrastructure;
using RestaurantManager.Models;
using System.Linq;

namespace RestaurantManager;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }
    public static RestaurantDbContext? DbContext { get; private set; }
    public static IOrderService? OrderService { get; private set; }
    public static IInventoryService? InventoryService { get; private set; }
    public static IUserService? UserService { get; private set; }
    public static ISyncService? SyncService { get; private set; }
    public static IBrandingService? BrandingService { get; private set; }
    public static ICategoryService? CategoryService { get; private set; }
    public static IDepartmentService? DepartmentService { get; private set; }
    public static User? CurrentUser { get; set; }
    public static ICategoryDepartmentService? CategoryDepartmentService { get; private set; }
    public static ISubCategoryService? SubCategoryService { get; private set; }
    public static IMenuItemService? MenuItemService { get; private set; }
    public static ILoggerService? Logger { get; private set; }
    public static INotificationService? Notifications { get; private set; }
    public static IUpdateService? UpdateService { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            Logger?.LogError(args.Exception, "Unexpected application error");
            MessageBox.Show($"Unexpected error:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            // Initialize Dependency Injection
            ServiceProvider = DependencyInjectionSetup.BuildServiceProvider();

            // Get services from DI container
            Logger = ServiceProvider.GetRequiredService<ILoggerService>();
            DbContext = ServiceProvider.GetRequiredService<RestaurantDbContext>();
            OrderService = ServiceProvider.GetRequiredService<IOrderService>();
            InventoryService = ServiceProvider.GetRequiredService<IInventoryService>();
            UserService = ServiceProvider.GetRequiredService<IUserService>();
            SyncService = ServiceProvider.GetRequiredService<ISyncService>();
            BrandingService = ServiceProvider.GetRequiredService<IBrandingService>();
            CategoryService = ServiceProvider.GetRequiredService<ICategoryService>();
            DepartmentService = ServiceProvider.GetRequiredService<IDepartmentService>();
            CategoryDepartmentService = ServiceProvider.GetRequiredService<ICategoryDepartmentService>();
            MenuItemService = ServiceProvider.GetRequiredService<IMenuItemService>();
            SubCategoryService = ServiceProvider.GetRequiredService<ISubCategoryService>();
            UpdateService = ServiceProvider.GetRequiredService<IUpdateService>();

            // Initialize notification service after main window is created
            MainWindow = new MainWindow();
            Notifications = new NotificationService();

            Logger.LogInformation("=== RestaurantManager Application Starting ===");

            // Initialize database
            InitializeDatabase();

            // Start sync service
            StartSyncService();

            // Show main window (may have been closed if user dismissed login)
            if (App.CurrentUser != null)
            {
                MainWindow.Show();
            }
            else
            {
                Shutdown();
                return;
            }

            Logger.LogInformation("Application started successfully");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Application startup failed");
            MessageBox.Show($"Startup error: {ex.Message}\n\nCheck logs for details.", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    public static async void ApplyLogoIcon(Window window)
    {
        try
        {
            var settings = await BrandingService!.GetSettingsAsync();
            var resolvedPath = Views.AdminSettingsView.ResolveImagePath(settings.LogoImagePath);
            if (resolvedPath != null)
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                window.Icon = bmp;
            }
        }
        catch (Exception ex) { Logger?.LogWarning(ex, "Failed to apply logo icon"); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger?.LogInformation("=== RestaurantManager Application Closing ===");
        DbContext?.Dispose();
        (ServiceProvider as IDisposable)?.Dispose();
        base.OnExit(e);
    }

    private void InitializeDatabase()
    {
        if (DbContext == null) return;

        try
        {
            DbContext.Database.EnsureCreated();

            // Ensure newer tables exist on databases created before they were added
            DbContext.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Categories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL DEFAULT '',
                    Icon TEXT NOT NULL DEFAULT '🏷',
                    Color TEXT NOT NULL DEFAULT '#667eea',
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01'
                );
                CREATE TABLE IF NOT EXISTS Departments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL DEFAULT '',
                    Icon TEXT NOT NULL DEFAULT '🏢',
                    Color TEXT NOT NULL DEFAULT '#667eea',
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01'
                );
                CREATE TABLE IF NOT EXISTS UserDepartments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    DepartmentId INTEGER NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_UserDepartments_UserId_DepartmentId ON UserDepartments(UserId, DepartmentId);
                CREATE TABLE IF NOT EXISTS CategoryDepartments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryId INTEGER NOT NULL,
                    DepartmentId INTEGER NOT NULL,
                    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE,
                    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_CategoryDepartments_CategoryId_DepartmentId ON CategoryDepartments(CategoryId, DepartmentId);
                CREATE TABLE IF NOT EXISTS SubCategories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL DEFAULT '',
                    CategoryId INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                    FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
                );
            ");

            // Migrate OrderItems: ensure InventoryItemId column exists
            try
            {
                // Check if the column already exists
                DbContext.Database.ExecuteSqlRaw("SELECT InventoryItemId FROM OrderItems LIMIT 0;");
            }
            catch
            {
                // Column doesn't exist – try renaming from old name
                try
                {
                    DbContext.Database.ExecuteSqlRaw("ALTER TABLE OrderItems RENAME COLUMN MenuItemId TO InventoryItemId;");
                }
                catch
                {
                    // Rename failed – recreate the table with correct schema
                    DbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS OrderItems;");
                    DbContext.Database.ExecuteSqlRaw(@"
                        CREATE TABLE OrderItems (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            OrderId INTEGER NOT NULL,
                            InventoryItemId INTEGER,
                            Quantity INTEGER NOT NULL DEFAULT 0,
                            Price TEXT NOT NULL DEFAULT '0',
                            Notes TEXT,
                            CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                            FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
                            FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems(Id) ON DELETE SET NULL
                        );
                    ");
                }
            }

            // Migrate OrderItems: make InventoryItemId nullable (for cascade-safe deletes)
            try
            {
                // Check if InventoryItemId is already nullable by inserting/rolling back a null test
                // SQLite can't alter columns, so we recreate the table if needed
                DbContext.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS OrderItems_new (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        OrderId INTEGER NOT NULL,
                        InventoryItemId INTEGER,
                        Quantity INTEGER NOT NULL DEFAULT 0,
                        Price TEXT NOT NULL DEFAULT '0',
                        Notes TEXT,
                        CreatedAt TEXT NOT NULL DEFAULT '0001-01-01',
                        FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
                        FOREIGN KEY (InventoryItemId) REFERENCES InventoryItems(Id) ON DELETE SET NULL
                    );
                    INSERT OR IGNORE INTO OrderItems_new (Id, OrderId, InventoryItemId, Quantity, Price, Notes, CreatedAt)
                        SELECT Id, OrderId, InventoryItemId, Quantity, Price, Notes, CreatedAt FROM OrderItems;
                    DROP TABLE OrderItems;
                    ALTER TABLE OrderItems_new RENAME TO OrderItems;
                ");
            }
            catch { /* migration already applied or table already correct */ }

            // Migrate BrandingSettings: add ReceiptHeader and ReceiptFooter columns
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ReceiptHeader TEXT NOT NULL DEFAULT '';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ReceiptFooter TEXT NOT NULL DEFAULT 'Thank you for your patronage!';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN PrintLogoOnReceipt INTEGER NOT NULL DEFAULT 1;"); } catch { }

            // Migrate BrandingSettings: add ShowDemoLogin column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ShowDemoLogin INTEGER NOT NULL DEFAULT 0;"); } catch { }
            // Force demo login off for existing databases
            try { DbContext.Database.ExecuteSqlRaw("UPDATE BrandingSettings SET ShowDemoLogin = 0 WHERE ShowDemoLogin = 1;"); } catch { }
            // Migrate BrandingSettings: add TrademarkText column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN TrademarkText TEXT NOT NULL DEFAULT 'Powered by Restaurant Manager';"); } catch { }
            // Migrate BrandingSettings: add TitleBarColor column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN TitleBarColor TEXT NOT NULL DEFAULT '#667eea';"); } catch { }
            // Migrate BrandingSettings: add TitleBarBackgroundColor column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN TitleBarBackgroundColor TEXT NOT NULL DEFAULT '#ffffff';"); } catch { }
            // Migrate BrandingSettings: add ProductCardSize column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ProductCardSize TEXT NOT NULL DEFAULT 'medium';"); } catch { }
            // Migrate BrandingSettings: add EnabledPaymentMethods column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN EnabledPaymentMethods TEXT NOT NULL DEFAULT 'cash,transfer,pos,cash_transfer,cash_pos';"); } catch { }
            // Migrate BrandingSettings: add ProductCardFontSize and ProductCardFontWeight
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ProductCardFontSize INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN ProductCardFontWeight TEXT NOT NULL DEFAULT 'SemiBold';"); } catch { }
            // Migrate BrandingSettings: add CategoryTabSize column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN CategoryTabSize TEXT NOT NULL DEFAULT 'medium';"); } catch { }

            // Migrate BrandingSettings: add UpdateCheckUrl column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN UpdateCheckUrl TEXT NOT NULL DEFAULT 'https://gist.githubusercontent.com/sireebos/48bb09c778e714e8182597986240d5e9/raw/2c3f82c10c7227183c2eaf0589fc6f51aee3ece4/version.json';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("UPDATE BrandingSettings SET UpdateCheckUrl = 'https://gist.githubusercontent.com/sireebos/48bb09c778e714e8182597986240d5e9/raw/2c3f82c10c7227183c2eaf0589fc6f51aee3ece4/version.json' WHERE UpdateCheckUrl = 'https://tanzahost.com/api/restaurant-manager/version.json' OR UpdateCheckUrl = '' OR UpdateCheckUrl IS NULL;"); } catch { }

            // Migrate BrandingSettings: sidebar and welcome screen customization
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN SidebarBgColor TEXT NOT NULL DEFAULT '#f8fafc';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN SidebarTextColor TEXT NOT NULL DEFAULT '#334155';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN SidebarBgImagePath TEXT;"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN SidebarNavActiveColor TEXT NOT NULL DEFAULT '#667eea';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN WelcomeBgColor TEXT NOT NULL DEFAULT '#ffffff';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN WelcomeTextColor TEXT NOT NULL DEFAULT '#0f172a';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN WelcomeBgImagePath TEXT;"); } catch { }

            // Login screen text customization
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN LoginButtonText TEXT NOT NULL DEFAULT 'Sign In';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN EmailLabel TEXT NOT NULL DEFAULT 'EMAIL';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN PasswordLabel TEXT NOT NULL DEFAULT 'PASSWORD';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN QuickLoginTitle TEXT NOT NULL DEFAULT 'QUICK LOGIN';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN PasswordHintText TEXT NOT NULL DEFAULT 'Password for all: password';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN FeaturePill1 TEXT NOT NULL DEFAULT '📋 Orders';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN FeaturePill2 TEXT NOT NULL DEFAULT '📦 Inventory';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN FeaturePill3 TEXT NOT NULL DEFAULT '📊 Reports';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN FeaturePillBgColor TEXT NOT NULL DEFAULT '#ffffff10';"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE BrandingSettings ADD COLUMN FeaturePillTextColor TEXT NOT NULL DEFAULT '#b0b0d0';"); } catch { }

            // Migrate Categories: add SortOrder column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE Categories ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;"); } catch { }

            // Migrate Orders: add PaymentMethod column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE Orders ADD COLUMN PaymentMethod TEXT;"); } catch { }
            // Backfill: set NULL PaymentMethod to 'cash' for completed orders
            try { DbContext.Database.ExecuteSqlRaw("UPDATE Orders SET PaymentMethod = 'cash' WHERE PaymentMethod IS NULL AND Status = 'completed';"); } catch { }

            // Migrate InventoryItems and MenuItems: add SubCategory column
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE InventoryItems ADD COLUMN SubCategory TEXT;"); } catch { }
            try { DbContext.Database.ExecuteSqlRaw("ALTER TABLE MenuItems ADD COLUMN SubCategory TEXT;"); } catch { }

            // Update dev account password
            try { DbContext.Database.ExecuteSqlRaw("UPDATE Users SET Password = '@June41988' WHERE Email = 'dev@restaurant.test';"); } catch { }

            SeedDatabase();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SeedDatabase()
    {
        if (DbContext == null) return;

        try
        {
            Logger?.LogInformation("Starting database seeding...");

            // Only seed users/menu/inventory if empty
            if (!DbContext.Users.Any())
            {
                var users = new[]
                {
                    new User { Name = "Developer", Email = "dev@restaurant.test", Password = "@June41988", Role = "developer", Status = "active" },
                    new User { Name = "Admin User", Email = "admin@restaurant.test", Password = "password", Role = "admin", Status = "active" },
                    new User { Name = "Chef User", Email = "chef@restaurant.test", Password = "password", Role = "staff", Department = "kitchen", Status = "active" },
                    new User { Name = "Waiter User", Email = "waiter@restaurant.test", Password = "password", Role = "staff", Department = "service", Status = "active" },
                    new User { Name = "Bartender User", Email = "bar@restaurant.test", Password = "password", Role = "staff", Department = "bar", Status = "active" },
                    new User { Name = "Cashier User", Email = "cashier@restaurant.test", Password = "password", Role = "staff", Department = "cashier", Status = "active" }
                };

                DbContext.Users.AddRange(users);
                DbContext.SaveChanges();
                Logger?.LogInformation("Seeded {UserCount} users", users.Length);
            }

            // Seed / update menu items from catalog (independent check)
            var needsMenuReseed = !DbContext.MenuItems.Any()
                || !DbContext.MenuItems.Any(m => m.Name == "Beliare Rose")
                || !DbContext.MenuItems.Any(m => m.SubCategory != null && m.SubCategory != "");
            if (needsMenuReseed)
            {
                var oldItems = DbContext.MenuItems.ToList();
                DbContext.MenuItems.RemoveRange(oldItems);
                DbContext.SaveChanges();

                var menuItems = RestaurantManager.Data.MenuSeedData.GetMenuItems();
                DbContext.MenuItems.AddRange(menuItems);
                DbContext.SaveChanges();
                Logger?.LogInformation("Seeded {MenuItemCount} menu items from catalog", menuItems.Length);
            }

            // Only seed inventory if empty or missing subcategories
            if (!DbContext.InventoryItems.Any() || !DbContext.InventoryItems.Any(i => i.SubCategory != null && i.SubCategory != ""))
            {
                var oldItems = DbContext.InventoryItems.ToList();
                DbContext.InventoryItems.RemoveRange(oldItems);
                DbContext.SaveChanges();

                var inventoryItems = RestaurantManager.Data.MenuSeedData.GetInventoryItems();
                DbContext.InventoryItems.AddRange(inventoryItems);
                DbContext.SaveChanges();
                Logger?.LogInformation("Seeded {InventoryItemCount} inventory items from catalog", inventoryItems.Length);
            }

            // Seed / update categories
            var needsCategoryReseed = !DbContext.Categories.Any() 
                || !DbContext.Categories.Any(c => c.Name == "Tama's Lounge");
            if (needsCategoryReseed)
            {
                // Clear old default categories
                var oldCategories = DbContext.Categories.ToList();
                DbContext.Categories.RemoveRange(oldCategories);
                DbContext.SaveChanges();

                var categories = new[]
                {
                    new Category { Name = "Tama's Lounge", Icon = "🍸", Color = "#8b5cf6", SortOrder = 1 },
                    new Category { Name = "Shawarma Spot", Icon = "🌯", Color = "#f97316", SortOrder = 2 },
                    new Category { Name = "Noodles Spot", Icon = "🍜", Color = "#eab308", SortOrder = 3 },
                    new Category { Name = "Eatery Menu", Icon = "🍽", Color = "#22c55e", SortOrder = 4 },
                    new Category { Name = "Drink Menu", Icon = "🥤", Color = "#3b82f6", SortOrder = 5 },
                    new Category { Name = "Pastries Menu", Icon = "🧁", Color = "#ec4899", SortOrder = 6 },
                    new Category { Name = "Barbecue Spot", Icon = "🔥", Color = "#ef4444", SortOrder = 7 },
                };
                DbContext.Categories.AddRange(categories);
                DbContext.SaveChanges();
                Logger?.LogInformation("Seeded {CategoryCount} categories", categories.Length);
            }

            // Seed departments independently if empty
            if (!DbContext.Departments.Any())
            {
                var departments = new[]
                {
                    new Department { Name = "Eatery/Restaurant", Icon = "🍽", Color = "#f97316" },
                    new Department { Name = "Lounge", Icon = "🛋", Color = "#8b5cf6" },
                    new Department { Name = "Shawarma Spot", Icon = "🌯", Color = "#ef4444" },
                };
                DbContext.Departments.AddRange(departments);
                DbContext.SaveChanges();
                Logger?.LogInformation("Seeded {DepartmentCount} departments", departments.Length);
            }

            // Seed sub-categories if empty (derive from items if any)
            if (!DbContext.SubCategories.Any())
            {
                var existingItems = DbContext.MenuItems.Where(m => !string.IsNullOrEmpty(m.SubCategory)).ToList();
                var cats = DbContext.Categories.ToList();
                var list = new List<SubCategory>();
                
                foreach (var item in existingItems.GroupBy(i => new { i.Category, i.SubCategory }))
                {
                    var cat = cats.FirstOrDefault(c => c.Name == item.Key.Category);
                    if (cat != null && !string.IsNullOrEmpty(item.Key.SubCategory))
                    {
                        list.Add(new SubCategory { Name = item.Key.SubCategory, CategoryId = cat.Id });
                    }
                }

                if (list.Any())
                {
                    DbContext.SubCategories.AddRange(list);
                    DbContext.SaveChanges();
                    Logger?.LogInformation("Seeded {SubCategoryCount} sub-categories from items", list.Count);
                }
            }

            Logger?.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Database seeding failed");
            MessageBox.Show($"Database error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartSyncService()
    {
        Logger?.LogInformation("Starting background sync service (interval: 30 seconds)");

        // Sync every 30 seconds using a dedicated DbContext per tick
        var syncTimer = new System.Timers.Timer(30000);
        syncTimer.AutoReset = true;
        syncTimer.Elapsed += async (s, e) =>
        {
            try
            {
                // Create a separate DbContext for this background thread
                // to avoid thread-safety issues with the UI DbContext
                using var syncDb = new RestaurantDbContext();
                var syncService = new SyncService(syncDb, Logger);
                var result = await syncService.SyncDataAsync();
                if (!result)
                {
                    Logger?.LogDebug("Sync completed but no changes were synced");
                }
                else
                {
                    Logger?.LogDebug("Sync completed successfully");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Background sync error occurred");
            }
        };
        syncTimer.Start();
        Logger?.LogInformation("Background sync service started");
    }
}
