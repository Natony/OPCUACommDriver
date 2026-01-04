using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Api;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services;
using OpcUaCommunicationEngine.Services.Auth;
using OpcUaCommunicationEngine.Services.OpcUa;
using OpcUaCommunicationEngine.ViewModels;
using OpcUaCommunicationEngine.Views;
using Serilog;

namespace OpcUaCommunicationEngine;

/// <summary>
/// Application entry point
/// Setup Dependency Injection và Logging
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ApiHostService? _apiHostService;
    private MainViewModel? _mainViewModel;

    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");
    public ApiHostService? ApiHost => _apiHostService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup initial logging (file and console only)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(
                path: "Logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== Application Starting ===");

        try
        {
            // Setup Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            Log.Information("Services configured successfully");

            // Create and show main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

            // Reconfigure logger to include UI sink
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.Debug()
                .WriteTo.File(
                    path: "Logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.UiSink(_mainViewModel.LogEntries, maxEntries: 500)
                .CreateLogger();

            Log.Information("UI Log sink configured");

            mainWindow.DataContext = _mainViewModel;
            mainWindow.Show();

            Log.Information("MainWindow shown");

            // Initialize ViewModel
            _ = InitializeViewModelAsync(_mainViewModel);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application failed to start");
            MessageBox.Show($"Application failed to start:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task InitializeViewModelAsync(MainViewModel viewModel)
    {
        try
        {
            Log.Information("Starting ViewModel initialization...");
            await Task.Delay(100);
            await viewModel.InitializeAsync();
            Log.Information("ViewModel initialization completed");

            // Start API server
            await StartApiServerAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during ViewModel initialization");

            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show($"Error loading configuration:\n{ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }
    }

    private async Task StartApiServerAsync()
    {
        try
        {
            _apiHostService = _serviceProvider?.GetService<ApiHostService>();
            if (_apiHostService != null)
            {
                await _apiHostService.StartAsync();
                Log.Information("API Server started successfully at {Url}", _apiHostService.BaseUrl);

                // Connect MainViewModel to LockService for UI updates
                if (_mainViewModel != null && _apiHostService.LockService != null)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _mainViewModel.SetLockService(_apiHostService.LockService);
                    });
                    Log.Information("Lock service connected to UI");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start API server");
            // API server failure should not prevent the app from running
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        Log.Debug("Configuring services...");

        // Logging
        services.AddSingleton<ILogger>(Log.Logger);

        // Load AuthSettings
        var authSettings = LoadAuthSettings();
        services.AddSingleton(authSettings);

        // Services
        services.AddSingleton<IConfigurationService>(sp =>
        {
            Log.Debug("Creating ConfigurationService...");
            return new ConfigurationService(sp.GetRequiredService<ILogger>());
        });

        services.AddSingleton<IDataCache, DataCacheService>();

        // OPC UA Manager
        services.AddSingleton<IPlcManager>(sp =>
        {
            Log.Debug("Creating PlcManager...");
            return new PlcManager(
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<IConfigurationService>(),
                sp.GetRequiredService<IDataCache>());
        });

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        // API Host Service
        services.AddSingleton<ApiHostService>(sp =>
        {
            Log.Debug("Creating ApiHostService...");
            return new ApiHostService(
                sp.GetRequiredService<IPlcManager>(),
                sp.GetRequiredService<ILogger>(),
                sp.GetRequiredService<AuthSettings>(),
                port: 5000);
        });

        Log.Debug("Services configured");
    }

    private AuthSettings LoadAuthSettings()
    {
        const string authSettingsPath = "Configurations/auth_settings.json";

        try
        {
            if (File.Exists(authSettingsPath))
            {
                var json = File.ReadAllText(authSettingsPath);
                var settings = JsonConvert.DeserializeObject<AuthSettings>(json);
                if (settings != null)
                {
                    Log.Information("Loaded auth settings from {Path}", authSettingsPath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading auth settings, using defaults");
        }

        Log.Information("Using default auth settings");
        return new AuthSettings();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Application Shutting Down ===");

        // Stop API server
        if (_apiHostService != null)
        {
            Log.Information("Stopping API server...");
            _apiHostService.StopAsync().GetAwaiter().GetResult();
            _apiHostService.Dispose();
        }

        // Dispose PlcManager
        if (_serviceProvider != null)
        {
            var plcManager = _serviceProvider.GetService<IPlcManager>();
            plcManager?.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
