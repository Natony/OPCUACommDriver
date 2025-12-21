using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpcUaCommunicationEngine.Api.Hubs;
using OpcUaCommunicationEngine.Interfaces;
using Serilog;
using System.Text.Json.Serialization;

namespace OpcUaCommunicationEngine.Api;

/// <summary>
/// Service to host ASP.NET Core API within WPF application
/// </summary>
public class ApiHostService : IDisposable
{
    private IHost? _host;
    private readonly IPlcManager _plcManager;
    private readonly ILogger _logger;
    private readonly int _port;
    private PlcHubService? _hubService;
    private bool _disposed;

    public bool IsRunning => _host != null;
    public string BaseUrl => $"http://localhost:{_port}";
    public PlcHubService? HubService => _hubService;

    public ApiHostService(IPlcManager plcManager, ILogger logger, int port = 5000)
    {
        _plcManager = plcManager;
        _logger = logger;
        _port = port;
    }

    /// <summary>
    /// Start the API server
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_host != null)
        {
            _logger.Warning("API server is already running");
            return;
        }

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"http://0.0.0.0:{_port}");
                    webBuilder.ConfigureServices(services =>
                    {
                        // Add controllers
                        services.AddControllers()
                            .AddJsonOptions(options =>
                            {
                                options.JsonSerializerOptions.PropertyNamingPolicy = null; // PascalCase
                                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                            });

                        // Add SignalR
                        services.AddSignalR()
                            .AddJsonProtocol(options =>
                            {
                                options.PayloadSerializerOptions.PropertyNamingPolicy = null;
                                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                            });

                        // Add CORS for mobile clients
                        services.AddCors(options =>
                        {
                            options.AddDefaultPolicy(policy =>
                            {
                                policy.AllowAnyOrigin()
                                      .AllowAnyMethod()
                                      .AllowAnyHeader();
                            });

                            options.AddPolicy("SignalR", policy =>
                            {
                                policy.AllowAnyMethod()
                                      .AllowAnyHeader()
                                      .AllowCredentials()
                                      .SetIsOriginAllowed(_ => true);
                            });
                        });

                        // Register dependencies
                        services.AddSingleton(_plcManager);
                        services.AddSingleton(_logger);
                        services.AddSingleton<PlcHubService>();

                        // Add Swagger for API documentation
                        services.AddEndpointsApiExplorer();
                        services.AddSwaggerGen(c =>
                        {
                            c.SwaggerDoc("v1", new()
                            {
                                Title = "OPC UA Communication Engine API",
                                Version = "v1",
                                Description = "REST API for controlling PLC connections and reading/writing tag values"
                            });
                        });
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();

                        // Enable CORS
                        app.UseCors();

                        // Enable Swagger
                        app.UseSwagger();
                        app.UseSwaggerUI(c =>
                        {
                            c.SwaggerEndpoint("/swagger/v1/swagger.json", "OPC UA API v1");
                            c.RoutePrefix = string.Empty; // Swagger at root
                        });

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapHub<PlcHub>("/hubs/plc").RequireCors("SignalR");
                        });
                    });
                })
                .Build();

            // Get the hub service
            _hubService = _host.Services.GetRequiredService<PlcHubService>();

            // Subscribe to PLC events
            SubscribeToPLcEvents();

            await _host.StartAsync(cancellationToken);

            _logger.Information("API Server started on {BaseUrl}", BaseUrl);
            _logger.Information("Swagger UI available at {BaseUrl}/index.html", BaseUrl);
            _logger.Information("SignalR Hub available at {BaseUrl}/hubs/plc", BaseUrl);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start API server");
            throw;
        }
    }

    /// <summary>
    /// Stop the API server
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_host == null) return;

        try
        {
            await _host.StopAsync(cancellationToken);
            _host.Dispose();
            _host = null;
            _hubService = null;

            _logger.Information("API Server stopped");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping API server");
        }
    }

    private void SubscribeToPLcEvents()
    {
        // Subscribe to tag value changes
        _plcManager.TagValueChanged += async (sender, args) =>
        {
            if (_hubService != null)
            {
                await _hubService.BroadcastTagValueAsync(
                    args.PlcId,
                    args.TagId,
                    args.NodeId,
                    args.Value.Value,
                    args.Value.Quality.ToString(),
                    args.Timestamp);
            }
        };

        // Subscribe to connection state changes
        _plcManager.ConnectionStateChanged += async (sender, args) =>
        {
            if (_hubService != null)
            {
                await _hubService.BroadcastConnectionStateAsync(
                    args.PlcId,
                    args.PlcName,
                    args.NewState.ToString(),
                    args.NewState == Enums.PlcConnectionState.Connected);
            }
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _host?.Dispose();
        _host = null;
    }
}
