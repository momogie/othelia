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
}
else
{
    builder.Services.AddSingleton<ITelemetryStore, NoopTelemetryStore>();
    builder.Services.AddSingleton<ISchemaInitializer, NoopSchemaInitializer>();
}

builder.Services.AddScoped<ITracingService, TracingService>();

// API on :5007, OTLP receiver on :4318 (overrides launchSettings/ASPNETCORE_URLS).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5007);
    options.ListenLocalhost(4318);
});

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
