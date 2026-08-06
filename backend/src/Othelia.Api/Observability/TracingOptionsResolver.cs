using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Othelia.Api.Observability;

public sealed class TracingSettingsPatch
{
    public bool? IngestionEnabled { get; set; }
    public string? IngestionEndpoint { get; set; }
    public List<IngestionFilterRule>? IngestionRules { get; set; }
    public int? QueryMaxTracesPerRequest { get; set; }
    public int? QueryMaxSpansPerTrace { get; set; }
    public int? QueryDefaultLookbackSeconds { get; set; }
    public int? QuerySlowThresholdMs { get; set; }
    public bool? AlertsEnabled { get; set; }
    public int? AlertsMaxAlerts { get; set; }
    public double? AlertsErrorRateDownThreshold { get; set; }
    public bool? MetricsEnabled { get; set; }
    public int? MetricsDefaultBucketSeconds { get; set; }
    public int? MetricsMaxSeriesPoints { get; set; }
    public int? ServiceMapWindowSeconds { get; set; }
    public int? CollectorsWindowSeconds { get; set; }
    public int? CollectorsStaleMinutes { get; set; }
    public int? LiveStreamIntervalSeconds { get; set; }
    public bool? RetentionEnabled { get; set; }
    public int? RetentionDays { get; set; }
    public int? RetentionCleanupIntervalHours { get; set; }
}

public interface ITracingOptionsResolver
{
    Task<TracingOptions> ResolveAsync(CancellationToken ct);
    Task SaveAsync(TracingSettingsPatch patch, CancellationToken ct);
    Task ResetAsync(CancellationToken ct);
}

public sealed class TracingOptionsResolver : ITracingOptionsResolver
{
    private const string SettingsKey = "Tracing";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISettingsStore _settings;
    private readonly IOptions<TracingOptions> _defaults;
    private readonly object _gate = new();

    private string? _cachedJson;
    private DateTime _cachedAtUtc;
    private TracingOptions? _cached;

    public TracingOptionsResolver(ISettingsStore settings, IOptions<TracingOptions> defaults)
    {
        _settings = settings;
        _defaults = defaults;
    }

    public async Task<TracingOptions> ResolveAsync(CancellationToken ct)
    {
        var json = await _settings.GetAsync(SettingsKey, ct);

        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (_cached is not null
                && _cachedJson == json
                && now - _cachedAtUtc < CacheTtl)
            {
                return Clone(_cached);
            }
        }

        var options = Clone(_defaults.Value);
        if (!string.IsNullOrWhiteSpace(json))
        {
            var patch = JsonSerializer.Deserialize<TracingSettingsPatch>(json, JsonOptions);
            if (patch is not null)
                Apply(options, patch);
        }

        lock (_gate)
        {
            _cached = Clone(options);
            _cachedJson = json;
            _cachedAtUtc = DateTime.UtcNow;
        }

        return options;
    }

    public async Task SaveAsync(TracingSettingsPatch patch, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(patch, JsonOptions);
        await _settings.SetAsync(SettingsKey, json, ct);
        Invalidate();
    }

    public Task ResetAsync(CancellationToken ct)
    {
        Invalidate();
        return _settings.DeleteAsync(SettingsKey, ct);
    }

    private void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
            _cachedJson = null;
        }
    }

    private static TracingOptions Clone(TracingOptions source)
        => JsonSerializer.Deserialize<TracingOptions>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions) ?? new TracingOptions();

    private static void Apply(TracingOptions options, TracingSettingsPatch patch)
    {
        if (patch.IngestionEnabled.HasValue)
            options.Ingestion.Enabled = patch.IngestionEnabled.Value;
        if (!string.IsNullOrWhiteSpace(patch.IngestionEndpoint))
            options.Ingestion.Endpoint = patch.IngestionEndpoint;
        if (patch.IngestionRules is not null)
            options.Ingestion.Rules = patch.IngestionRules;
        if (patch.QueryMaxTracesPerRequest.HasValue)
            options.Query.MaxTracesPerRequest = patch.QueryMaxTracesPerRequest.Value;
        if (patch.QueryMaxSpansPerTrace.HasValue)
            options.Query.MaxSpansPerTrace = patch.QueryMaxSpansPerTrace.Value;
        if (patch.QueryDefaultLookbackSeconds.HasValue)
            options.Query.DefaultLookbackSeconds = patch.QueryDefaultLookbackSeconds.Value;
        if (patch.QuerySlowThresholdMs.HasValue)
            options.Query.SlowThresholdMs = patch.QuerySlowThresholdMs.Value;
        if (patch.AlertsEnabled.HasValue)
            options.Alerts.Enabled = patch.AlertsEnabled.Value;
        if (patch.AlertsMaxAlerts.HasValue)
            options.Alerts.MaxAlerts = patch.AlertsMaxAlerts.Value;
        if (patch.AlertsErrorRateDownThreshold.HasValue)
            options.Alerts.ErrorRateDownThreshold = patch.AlertsErrorRateDownThreshold.Value;
        if (patch.MetricsEnabled.HasValue)
            options.Metrics.Enabled = patch.MetricsEnabled.Value;
        if (patch.MetricsDefaultBucketSeconds.HasValue)
            options.Metrics.DefaultBucketSeconds = patch.MetricsDefaultBucketSeconds.Value;
        if (patch.MetricsMaxSeriesPoints.HasValue)
            options.Metrics.MaxSeriesPoints = patch.MetricsMaxSeriesPoints.Value;
        if (patch.ServiceMapWindowSeconds.HasValue)
            options.ServiceMap.WindowSeconds = patch.ServiceMapWindowSeconds.Value;
        if (patch.CollectorsWindowSeconds.HasValue)
            options.Collectors.WindowSeconds = patch.CollectorsWindowSeconds.Value;
        if (patch.CollectorsStaleMinutes.HasValue)
            options.Collectors.StaleMinutes = patch.CollectorsStaleMinutes.Value;
        if (patch.LiveStreamIntervalSeconds.HasValue)
            options.Live.StreamIntervalSeconds = patch.LiveStreamIntervalSeconds.Value;
        if (patch.RetentionEnabled.HasValue)
            options.Retention.Enabled = patch.RetentionEnabled.Value;
        if (patch.RetentionDays.HasValue)
            options.Retention.RetentionDays = patch.RetentionDays.Value;
        if (patch.RetentionCleanupIntervalHours.HasValue)
            options.Retention.CleanupIntervalHours = patch.RetentionCleanupIntervalHours.Value;
    }
}
