using System;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManager.Data;

namespace RestaurantManager.Services.Infrastructure;

/// <summary>
/// Centralized dependency injection configuration
/// </summary>
public static class DependencyInjectionSetup
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Database context
        services.AddScoped<RestaurantDbContext>();

        // Data services
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<ICategoryDepartmentService, CategoryDepartmentService>();
        services.AddScoped<ISubCategoryService, SubCategoryService>();
        services.AddScoped<IMenuItemService, MenuItemService>();

        // Infrastructure services
        services.AddSingleton<ILoggerService, LoggerService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddScoped<IValidationService, ValidationService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }

    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        return services.BuildServiceProvider();
    }
}
