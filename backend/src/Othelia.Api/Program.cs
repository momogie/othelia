using Othelia.Api;
using Othelia.Api.Observability;
using Othelia.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.Configure<TracingOptions>(builder.Configuration.GetSection("Tracing"));

var tracingOptions = builder.Configuration.GetSection("Tracing").Get<TracingOptions>() ?? new TracingOptions();
var connectionString = tracingOptions.Storage.ConnectionString;
var useSqlServer = string.Equals(tracingOptions.Storage.Provider, "SqlServer", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(connectionString);

if (useSqlServer)
{
    builder.Services.AddSingleton<ITelemetryStore>(new TelemetryStore(connectionString!));
    builder.Services.AddSingleton<ISchemaInitializer>(new SchemaInitializer(connectionString!));
    builder.Services.AddSingleton<ISettingsStore>(new SqlServerSettingsStore(connectionString!));
}
else
{
    builder.Services.AddSingleton<ITelemetryStore, NoopTelemetryStore>();
    builder.Services.AddSingleton<ISchemaInitializer, NoopSchemaInitializer>();
    builder.Services.AddSingleton<ISettingsStore, NoopSettingsStore>();
}

builder.Services.AddSingleton<ITracingOptionsResolver, TracingOptionsResolver>();
builder.Services.AddScoped<ITracingService, TracingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHostedService<RetentionService>();

// API + OTLP receiver binding dari config (override launchSettings/ASPNETCORE_URLS).
// Host: "localhost" | "0.0.0.0"/"any" | IP tertentu. Untuk AppAPI di server lain,
// set Tracing:Receiver:Host = "0.0.0.0".
builder.WebHost.ConfigureKestrel(options =>
{
    var apiHost = builder.Configuration["Tracing:Api:Host"] ?? "localhost";
    var apiPort = builder.Configuration.GetValue<int?>("Tracing:Api:Port") ?? 5007;
    var receiverHost = builder.Configuration["Tracing:Receiver:Host"] ?? "localhost";
    var receiverPort = builder.Configuration.GetValue<int?>("Tracing:Receiver:Port") ?? 4318;

    Listen(options, apiHost, apiPort);
    Listen(options, receiverHost, receiverPort);
});

static void Listen(Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions options, string host, int port)
{
    if (host.Equals("any", StringComparison.OrdinalIgnoreCase) || host is "0.0.0.0" or "*")
        options.Listen(System.Net.IPAddress.Any, port);
    else if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1")
        options.ListenLocalhost(port);
    else
        options.Listen(System.Net.IPAddress.Parse(host), port);
}

var app = builder.Build();

if (!useSqlServer)
{
    app.Logger.LogWarning("Telemetry storage not configured; OTLP data will be accepted but not persisted.");
}

using (var scope = app.Services.CreateScope())
{
    var schemaInitializer = scope.ServiceProvider.GetRequiredService<ISchemaInitializer>();
    try
    {
        await schemaInitializer.EnsureSchemaAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize telemetry schema.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendDev");

app.UseAuthorization();

app.MapControllers();

app.MapOtlpEndpoints();

app.Run();
