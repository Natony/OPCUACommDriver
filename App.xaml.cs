using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Services;
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

    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup Logging
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
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            
            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();

            Log.Information("MainWindow shown");

            // Initialize ViewModel
            _ = InitializeViewModelAsync(mainViewModel);
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

    private void ConfigureServices(IServiceCollection services)
    {
        Log.Debug("Configuring services...");
        
        // Logging
        services.AddSingleton<ILogger>(Log.Logger);

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
        
        Log.Debug("Services configured");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Application Shutting Down ===");
        
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
