
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using RestaurantManager.Models;
using RestaurantManager.Data;
using RestaurantManager.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace RestaurantManager.Services;

public interface ISyncService
{
    Task<bool> SyncDataAsync();
    Task<bool> IsOnlineAsync();
    Task<(int Pending, DateTime? LastSync)> GetSyncStatusAsync();
}

public class SyncService : ISyncService
{
    private readonly HttpClient _httpClient;
    private readonly RestaurantDbContext _dbContext;
    private readonly ILoggerService? _logger;
    private readonly string _apiBaseUrl = "http://localhost:8000/api";

    public SyncService(RestaurantDbContext dbContext)
    {
        _dbContext = dbContext;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public SyncService(RestaurantDbContext dbContext, ILoggerService? logger) : this(dbContext)
    {
        _logger = logger;
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(int Pending, DateTime? LastSync)> GetSyncStatusAsync()
    {
        var pending = await _dbContext.SyncLogs.CountAsync(s => !s.IsSynced);
        var lastSync = await _dbContext.SyncLogs
            .Where(s => s.IsSynced)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        return (pending, lastSync);
    }

    public async Task<bool> SyncDataAsync()
    {
        try
        {
            if (!await IsOnlineAsync())
            {
                _logger?.LogDebug("Offline - skipping sync");
                return false;
            }

            // Get pending changes
            var pendingChanges = await _dbContext.SyncLogs
                .Where(s => !s.IsSynced)
                .ToListAsync();

            if (pendingChanges.Count == 0)
            {
                _logger?.LogDebug("No changes to sync");
                await FetchLatestDataAsync();
                return true;
            }

            // Send changes to server
            var syncPayload = new
            {
                changes = pendingChanges.Select(c => new
                {
                    entity_type = c.EntityType,
                    entity_id = c.EntityId,
                    action = c.Action,
                    data = c.Data
                })
            };

            var json = JsonSerializer.Serialize(syncPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_apiBaseUrl}/sync", content);

            if (response.IsSuccessStatusCode)
            {
                // Mark as synced
                foreach (var change in pendingChanges)
                {
                    change.IsSynced = true;
                }
                await _dbContext.SaveChangesAsync();

                _logger?.LogInformation("Synced {ChangeCount} changes", pendingChanges.Count);

                // Fetch latest data
                await FetchLatestDataAsync();

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Sync error occurred");
            return false;
        }
    }

    private async Task FetchLatestDataAsync()
    {
        try
        {
            // Fetch orders
            await FetchAndUpdateAsync("orders",
                async (data) =>
                {
                    var orders = JsonSerializer.Deserialize<List<Order>>(data);
                    if (orders != null)
                    {
                        foreach (var order in orders)
                        {
                            var existing = await _dbContext.Orders.FindAsync(order.Id);
                            if (existing == null)
                            {
                                _dbContext.Orders.Add(order);
                            }
                            else
                            {
                                _dbContext.Entry(existing).CurrentValues.SetValues(order);
                            }
                        }
                        await _dbContext.SaveChangesAsync();
                    }
                });

            // Fetch inventory
            await FetchAndUpdateAsync("inventory",
                async (data) =>
                {
                    var items = JsonSerializer.Deserialize<List<InventoryItem>>(data);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var existing = await _dbContext.InventoryItems.FindAsync(item.Id);
                            if (existing == null)
                            {
                                _dbContext.InventoryItems.Add(item);
                            }
                            else
                            {
                                _dbContext.Entry(existing).CurrentValues.SetValues(item);
                            }
                        }
                        await _dbContext.SaveChangesAsync();
                    }
                });

            // Fetch menu items
            await FetchAndUpdateAsync("menu-items",
                async (data) =>
                {
                    var items = JsonSerializer.Deserialize<List<MenuItem>>(data);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            var existing = await _dbContext.MenuItems.FindAsync(item.Id);
                            if (existing == null)
                            {
                                _dbContext.MenuItems.Add(item);
                            }
                            else
                            {
                                _dbContext.Entry(existing).CurrentValues.SetValues(item);
                            }
                        }
                        await _dbContext.SaveChangesAsync();
                    }
                });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch latest data from server");
        }
    }

    private async Task FetchAndUpdateAsync(string endpoint, Func<string, Task> updateAction)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/{endpoint}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                // Try to extract a "data" property, or use the root
                string dataArray;
                if (doc.RootElement.TryGetProperty("data", out var dataProp))
                    dataArray = dataProp.GetRawText();
                else
                    dataArray = content;
                await updateAction(dataArray);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch {Endpoint}", endpoint);
        }
    }
}
