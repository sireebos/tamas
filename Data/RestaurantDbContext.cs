using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using RestaurantManager.Models;

namespace RestaurantManager.Data;

public class RestaurantDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<InventoryLog> InventoryLogs { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<UserDepartment> UserDepartments { get; set; }
    public DbSet<SyncLog> SyncLogs { get; set; }
    public DbSet<BrandingSettings> BrandingSettings { get; set; }
    public DbSet<CategoryDepartment> CategoryDepartments { get; set; }
    public DbSet<SubCategory> SubCategories { get; set; }

    public string DbPath { get; }

    public RestaurantDbContext()
    {
        var folder = Environment.SpecialFolder.ApplicationData;
        var path = Path.Combine(Environment.GetFolderPath(folder), "RestaurantManager");
        DbPath = Path.Join(path, "restaurant.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!Directory.Exists(Path.GetDirectoryName(DbPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        }
        options.UseSqlite($"Data Source={DbPath}");

        // Enable WAL mode for better concurrent read/write performance
        var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath}");
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }
        connection.Close();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.InventoryItem)
            .WithMany()
            .HasForeignKey(oi => oi.InventoryItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // Create indexes for better query performance
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CreatedAt);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<UserDepartment>()
            .HasOne(ud => ud.User)
            .WithMany()
            .HasForeignKey(ud => ud.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserDepartment>()
            .HasOne(ud => ud.Department)
            .WithMany(d => d.UserDepartments)
            .HasForeignKey(ud => ud.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserDepartment>()
            .HasIndex(ud => new { ud.UserId, ud.DepartmentId })
            .IsUnique();

        modelBuilder.Entity<CategoryDepartment>()
            .HasOne(cd => cd.Category)
            .WithMany(c => c.CategoryDepartments)
            .HasForeignKey(cd => cd.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CategoryDepartment>()
            .HasOne(cd => cd.Department)
            .WithMany()
            .HasForeignKey(cd => cd.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CategoryDepartment>()
            .HasIndex(cd => new { cd.CategoryId, cd.DepartmentId })
            .IsUnique();

        modelBuilder.Entity<SubCategory>()
            .HasOne(sc => sc.Category)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
