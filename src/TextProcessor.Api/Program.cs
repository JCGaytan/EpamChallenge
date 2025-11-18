using Serilog;
using Serilog.Events;
using TextProcessor.Api.HealthChecks;
using TextProcessor.Api.Hubs;
using TextProcessor.Api.Services;
using TextProcessor.Core.Extensions;
using TextProcessor.Core.Interfaces;

ConfigureSerilog();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

ConfigureServices(builder);

var app = builder.Build();

ConfigurePipeline(app);

try
{
    Log.Information("Starting TextProcessor API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TextProcessor API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureSerilog()
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File("logs/textprocessor-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 100_000_000,
            rollOnFileSizeLimit: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .CreateLogger();
}

static void ConfigureServices(WebApplicationBuilder builder)
{
    var services = builder.Services;

    services.AddControllers();
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "TextProcessor API",
            Version = "v1",
            Description = "A production-ready API for processing text with real-time character streaming"
        });
    });

    services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    });

    ConfigureCors(services);

    services.AddSingleton<BackgroundJobService>();
    services.AddHostedService<BackgroundJobService>(provider =>
        provider.GetRequiredService<BackgroundJobService>());

    services.AddTextProcessingCore();
    services.AddScoped<IBackgroundJobProcessor, BuiltInJobProcessor>();
    services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();
    services.AddSingleton<IMetricsService, InMemoryMetricsService>();

    var signalRHealthUrl = builder.Configuration["SignalRHealthUrl"] ?? "http://localhost:5133/hubs/processing";

    services.AddHealthChecks()
        .AddCheck<TextProcessingHealthCheck>("textprocessing")
        .AddCheck<JobManagerHealthCheck>("jobmanager")
        .AddSignalRHub(signalRHealthUrl, "signalr-hub");

    services.AddHealthChecksUI(opt =>
    {
        opt.SetEvaluationTimeInSeconds(30);
        opt.MaximumHistoryEntriesPerEndpoint(50);
        opt.SetApiMaxActiveRequests(2);
        opt.AddHealthCheckEndpoint("TextProcessor API", "/health");
    }).AddInMemoryStorage();
}

static void ConfigureCors(IServiceCollection services)
{
    services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        });

        options.AddPolicy("DevelopmentCors", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000", "http://127.0.0.1:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        });
    });
}

static void ConfigurePipeline(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        ConfigureSwagger(app);
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(ConfigureRequestLogging);
    AddSecurityHeaders(app);
    app.UseMiddleware<MetricsMiddleware>();
    app.UseRouting();
    app.UseCors(app.Environment.IsDevelopment() ? "DevelopmentCors" : "AllowReactApp");
    MapEndpoints(app);
}

static void ConfigureSwagger(WebApplication app)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TextProcessor API V1");
        c.RoutePrefix = string.Empty;
    });
}

static void ConfigureRequestLogging(Serilog.AspNetCore.RequestLoggingOptions options)
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : elapsed > 10000
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
    };
}

static void AddSecurityHeaders(IApplicationBuilder app)
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("X-Powered-By", "TextProcessor API");
        await next();
    });
}

static void MapEndpoints(WebApplication app)
{
    app.MapControllers();
    app.MapHub<ProcessingHub>("/hubs/processing").RequireCors(app.Environment.IsDevelopment() ? "DevelopmentCors" : "AllowReactApp");

    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    data = x.Value.Data,
                    duration = x.Value.Duration.ToString()
                }),
                totalDuration = report.TotalDuration.ToString(),
                timestamp = DateTime.UtcNow
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.MapHealthChecksUI(options =>
        {
            options.UIPath = "/health-ui";
            options.ApiPath = "/health-ui-api";
        });
    }

    app.MapGet("/ping", () => new
    {
        Status = "OK",
        Timestamp = DateTime.UtcNow,
        Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
        Environment = app.Environment.EnvironmentName
    });
}

public partial class Program { }
