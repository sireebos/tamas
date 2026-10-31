# Restaurant Manager - C# WPF Desktop App

A modern, offline-first desktop application for restaurant order and inventory management built with **C# WPF** and Entity Framework Core.

## ✨ Features

✅ **Offline-First**: Works completely without internet connection
✅ **Auto-Sync**: Syncs with Laravel backend when online  
✅ **SQLite Database**: Fast local data storage
✅ **Role-Based Access**: Admin, Manager, Chef, Waiter roles
✅ **Modern UI**: Beautiful WPF interface
✅ **Orders Management**: Create, track, and manage orders
✅ **Inventory Management**: Track stock and low-stock alerts

## 🛠️ Requirements

- **Visual Studio 2022** (or later) with .NET 8 SDK
- **.NET 8.0** runtime
- **SQLite** (included in Entity Framework Core)

## 📦 Installation

### 1. Open in Visual Studio

1. Open Visual Studio 2022
2. File → Open Project
3. Navigate to `RestaurantManager.csproj`
4. Open the project

### 2. Restore NuGet Packages

```bash
cd RestaurantManager
dotnet restore
```

### 3. Build the Application

```bash
dotnet build
```

## 🚀 Running the App

### From Visual Studio

Press **F5** or click the "Run" button to start debugging.

### From Command Line

```bash
dotnet run
```

### Standalone Exe

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/RestaurantManager.exe`

## 📝 Demo Users

All users have password: `password`

- **admin@hotel.test** - Full system access
- **manager@hotel.test** - Operations management
- **chef@hotel.test** - Order preparation
- **waiter@hotel.test** - Order creation

## 📁 Project Structure

```
RestaurantManager/
├── Models/
│   └── DataModels.cs          # Entity models
├── Data/
│   └── RestaurantDbContext.cs # Database context
├── Services/
│   ├── DataServices.cs        # Business logic
│   └── SyncService.cs         # Sync with Laravel API
├── Views/
│   ├── LoginWindow.xaml       # Login screen
│   ├── DashboardView.xaml     # Dashboard
│   ├── OrdersView.xaml        # Orders management
│   └── InventoryView.xaml     # Inventory management
├── App.xaml                   # Application resources
├── MainWindow.xaml            # Main window
└── RestaurantManager.csproj   # Project file
```

## 💾 Database

### Location

- **Windows**: `%APPDATA%\RestaurantManager\restaurant.db`
- **Example**: `C:\Users\Username\AppData\Roaming\RestaurantManager\restaurant.db`

### Tables

- `Users` - User accounts and roles
- `Orders` - Customer orders
- `OrderItems` - Order line items
- `MenuItems` - Restaurant menu
- `InventoryItems` - Inventory products
- `InventoryLogs` - Stock transactions
- `SyncLogs` - Changes pending sync

## 🔄 Offline & Sync

### How It Works

1. **Offline**: All changes stored in local SQLite database
2. **Tracked**: Changes logged in `SyncLogs` table
3. **Auto-Sync**: Every 30 seconds when online
4. **Conflict Resolution**: Server data takes precedence

### Manual Sync

The app automatically syncs when it detects internet connection. To configure sync:

Edit `SyncService.cs` to change the API URL:
```csharp
private readonly string _apiBaseUrl = "http://localhost:8000/api";
```

Or use environment variable:
```csharp
private readonly string _apiBaseUrl = Environment.GetEnvironmentVariable("RESTAURANT_API_URL") ?? "http://localhost:8000/api";
```

## 🐛 Troubleshooting

### App won't start

1. Ensure .NET 8 SDK is installed:
   ```bash
   dotnet --version
   ```

2. Restore packages:
   ```bash
   dotnet restore
   ```

3. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

### Database issues

1. Delete database file:
   ```
   C:\Users\[Username]\AppData\Roaming\RestaurantManager\restaurant.db
   ```

2. Restart app - new database will be created with demo data

### Sync not working

1. Verify Laravel API is running on `http://localhost:8000`
2. Check internet connection
3. Review debug output in Visual Studio

## 🔧 Configuration

### API Connection

Edit `SyncService.cs`:
```csharp
private readonly string _apiBaseUrl = "http://your-server.com/api";
```

### Sync Interval

Edit `App.xaml.cs` in `StartSyncService()`:
```csharp
var syncTimer = new System.Timers.Timer(30000); // 30 seconds
```

### Database Location

Edit `RestaurantDbContext.cs`:
```csharp
public RestaurantDbContext()
{
    var folder = Environment.SpecialFolder.ApplicationData;
    var path = Path.Combine(Environment.GetFolderPath(folder), "RestaurantManager");
    DbPath = Path.Join(path, "restaurant.db");
}
```

## 📦 Building Installer (Advanced)

You can create an MSI or EXE installer using:

1. **WiX Toolset** - Advanced Windows Installer
2. **Advanced Installer** - Professional installer tool
3. **NSIS** - Nullsoft Scriptable Install System

For now, distribute the standalone EXE from the publish folder.

## 🔐 Security Notes

- Passwords are stored in plain text for demo - use bcrypt or hashing in production
- API communication should use HTTPS in production
- Implement proper authentication and authorization

## 🤝 Contributing

Feel free to extend this app with:
- Additional reports and analytics
- Advanced filtering and search
- Receipt printing
- Multi-location support
- More sophisticated conflict resolution

## 📝 License

MIT License - Use freely

---

**Ready to go!** Open in Visual Studio and press F5! 🚀
