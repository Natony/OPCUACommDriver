using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpcUaCommunicationEngine.Api.Hubs;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Models;
using OpcUaCommunicationEngine.Services.Auth;
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
    private readonly string _bindAddress;
    private readonly AuthSettings _authSettings;
    private PlcHubService? _hubService;
    private OperatorLockService? _lockService;
    private UserService? _userService;
    private bool _disposed;

    public bool IsRunning => _host != null;
    public string BindUrl => $"http://{_bindAddress}:{_port}";
    public string BaseUrl => _bindAddress == "0.0.0.0" ? $"http://localhost:{_port}" : BindUrl;
    public PlcHubService? HubService => _hubService;
    public OperatorLockService? LockService => _lockService;
    public UserService? UserService => _userService;

    public ApiHostService(IPlcManager plcManager, ILogger logger, AuthSettings authSettings, int port = 5000, string bindAddress = "0.0.0.0")
    {
        _plcManager = plcManager;
        _logger = logger;
        _authSettings = authSettings;
        _port = port;
        _bindAddress = bindAddress;
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
            // Create services that need to be shared
            _userService = new UserService(_authSettings);
            var authService = new AuthService(_authSettings, _userService);
            _lockService = new OperatorLockService(_authSettings);

            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"http://{_bindAddress}:{_port}");
                    webBuilder.ConfigureServices(services =>
                    {
                        // Add controllers
                        services.AddControllers()
                            .AddJsonOptions(options =>
                            {
                                options.JsonSerializerOptions.PropertyNamingPolicy = null; // PascalCase
                                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                            });

                        // Add JWT Authentication
                        if (_authSettings.EnableAuthentication)
                        {
                            services.AddAuthentication(options =>
                            {
                                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                            })
                            .AddJwtBearer(options =>
                            {
                                options.TokenValidationParameters = authService.GetTokenValidationParameters();

                                // Configure for SignalR
                                options.Events = new JwtBearerEvents
                                {
                                    OnMessageReceived = context =>
                                    {
                                        var accessToken = context.Request.Query["access_token"];
                                        var path = context.HttpContext.Request.Path;

                                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                        {
                                            context.Token = accessToken;
                                        }

                                        return Task.CompletedTask;
                                    }
                                };
                            });

                            services.AddAuthorization();
                        }

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
                        services.AddSingleton(_authSettings);
                        services.AddSingleton(_userService);
                        services.AddSingleton(authService);
                        services.AddSingleton(_lockService);
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

                            // Add JWT authentication to Swagger
                            if (_authSettings.EnableAuthentication)
                            {
                                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                                {
                                    Name = "Authorization",
                                    Type = SecuritySchemeType.ApiKey,
                                    Scheme = "Bearer",
                                    BearerFormat = "JWT",
                                    In = ParameterLocation.Header,
                                    Description = "Enter 'Bearer' followed by a space and your JWT token.\n\nExample: Bearer eyJhbGciOiJIUzI1NiIs..."
                                });

                                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                                {
                                    {
                                        new OpenApiSecurityScheme
                                        {
                                            Reference = new OpenApiReference
                                            {
                                                Type = ReferenceType.SecurityScheme,
                                                Id = "Bearer"
                                            }
                                        },
                                        Array.Empty<string>()
                                    }
                                });
                            }
                        });
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();

                        // Enable CORS
                        app.UseCors();

                        // Enable Authentication & Authorization
                        if (_authSettings.EnableAuthentication)
                        {
                            app.UseAuthentication();
                            app.UseAuthorization();
                        }

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

            // Subscribe to lock events
            SubscribeToLockEvents();

            await _host.StartAsync(cancellationToken);

            _logger.Information("API Server started on {BaseUrl}", BaseUrl);
            _logger.Information("Swagger UI available at {BaseUrl}/index.html", BaseUrl);
            _logger.Information("SignalR Hub available at {BaseUrl}/hubs/plc", BaseUrl);
            _logger.Information("Authentication: {Status}", _authSettings.EnableAuthentication ? "Enabled" : "Disabled");

            if (_authSettings.EnableAuthentication)
            {
                _logger.Information("Default admin account: admin / admin123");
            }
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

    private void SubscribeToLockEvents()
    {
        if (_lockService == null || _hubService == null) return;

        _lockService.LockAcquired += async (sender, args) =>
        {
            if (_hubService != null)
            {
                await _hubService.BroadcastLockAcquiredAsync(
                    args.Lock.UserId,
                    args.Lock.Username,
                    args.Lock.DisplayName,
                    args.Lock.ExpiresAt);
            }
        };

        _lockService.LockReleased += async (sender, args) =>
        {
            if (_hubService != null)
            {
                await _hubService.BroadcastLockReleasedAsync(
                    args.Lock.Username,
                    args.Reason);
            }
        };

        _lockService.LockExtended += async (sender, args) =>
        {
            if (_hubService != null)
            {
                await _hubService.BroadcastLockExtendedAsync(
                    args.Lock.UserId,
                    args.Lock.ExpiresAt);
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
