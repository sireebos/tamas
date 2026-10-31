# C# WPF Restaurant Manager - Quick Start

Your Windows desktop app is ready to build and run! Choose your method below.

Stop-Process -Name "RestaurantManager" -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; dotnet build 2>&1 | Select-Object -Last 15
dotnet rundotnet rundotnet run
## 🚀 Quick Start (Recommended)

### 1. Open in Visual Studio 2022

1. Launch **Visual Studio 2022**
2. Select **"Open a project or solution"**
3. Navigate to: `c:\Users\SireTheo\Desktop\laravel\hotel_booking\RestaurantManager`
4. Select **`RestaurantManager.csproj`**
5. Click **Open**

### 2. Build & Run

Press **F5** or go to **Debug → Start Debugging**

That's it! The app will:
- Create the database automatically
- Load demo data
- Show the login screen
- Fully functional offline!

## 📝 Demo Login

**Email**: admin@hotel.test  
**Password**: password

Try other users too:
- manager@hotel.test
- chef@hotel.test
- waiter@hotel.test

## 🎯 What's Included

✅ **Fully Functional App**
- Login system
- Dashboard with stats
- Orders management
- Inventory management

✅ **Offline-First**
- SQLite database
- Auto-sync with Laravel API
- Works without internet

✅ **Modern UI**
- Beautiful WPF interface
- Responsive design
- Professional look

## 🔧 Troubleshooting

### "Project type is not supported"

**Solution**: Install .NET 8 SDK
```bash
# Check if installed
dotnet --version

# Should show: 8.0.x or higher
```

### NuGet packages not found

**Solution**: Restore packages
```bash
cd RestaurantManager
dotnet restore
```

### Database errors

**Solution**: Delete database and restart
1. Close the app
2. Delete: `C:\Users\[YourName]\AppData\Roaming\RestaurantManager\restaurant.db`
3. Run the app again

## 📦 Building Standalone Exe

```bash
# Navigate to project
cd c:\Users\SireTheo\Desktop\laravel\hotel_booking\RestaurantManager

# Build release version
dotnet build -c Release

# Run the exe
.\bin\Release\net8.0-windows\RestaurantManager.exe
```

## 🌐 Connecting to Laravel API

The app connects to your Laravel API for syncing. Make sure:

1. Laravel app is running: `php artisan serve`
2. API is on: `http://localhost:8000/api`

To change API URL, edit `SyncService.cs`:
```csharp
private readonly string _apiBaseUrl = "http://localhost:8000/api";
```

## 📋 Features to Test

1. **Login Page**
   - Try admin@hotel.test / password
   - Try other demo users

2. **Dashboard**
   - View stats
   - See recent orders
   - Check inventory

3. **Orders Management**
   - View all orders
   - Filter by status
   - Delete orders (test)

4. **Inventory Management**
   - View all inventory items
   - Filter low stock
   - See stock levels

5. **Offline Mode**
   - Disconnect from internet
   - Make changes (they'll sync when online)
   - See sync status

## 🎓 Next Steps

1. **Customize Look & Feel**
   - Edit colors in `App.xaml`
   - Modify forms in Views

2. **Add Features**
   - Print receipts
   - Export reports
   - Multi-user support

3. **Deploy**
   - Create installer with WiX Toolset
   - Distribute to team
   - Auto-updates

## 💡 Tips

- **F5** = Run with debugging
- **Ctrl+F5** = Run without debugging
- **F12** = Open developer tools (if applicable)
- **Ctrl+R** = Refresh data

## 🆘 Support

If you encounter issues:

1. Check the README.md in the RestaurantManager folder
2. Verify .NET 8 SDK is installed
3. Ensure Laravel API is running
4. Delete database and restart

---

**You're all set!** Press F5 and enjoy your new desktop app! 🎉
