using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RestaurantManager.Services.Infrastructure;

/// <summary>
/// Centralized logging service using Serilog
/// </summary>
public interface ILoggerService
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogWarning(Exception ex, string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
    void LogDebug(string message, params object[] args);
    void LogTrace(string message, params object[] args);
}

public class LoggerService : ILoggerService
{
    private readonly ILogger _logger;

    public LoggerService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RestaurantManager",
            "Logs"
        );

        Directory.CreateDirectory(appDataPath);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .WriteTo.File(
                path: Path.Combine(appDataPath, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }

    public void LogInformation(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void LogWarning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void LogWarning(Exception ex, string message, params object[] args)
    {
        _logger.Warning(ex, message, args);
    }

    public void LogError(Exception ex, string message, params object[] args)
    {
        _logger.Error(ex, message, args);
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }

    public void LogTrace(string message, params object[] args)
    {
        _logger.Verbose(message, args);
    }
}
