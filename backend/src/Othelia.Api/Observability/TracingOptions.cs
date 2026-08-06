namespace Othelia.Api.Observability;

public sealed class TracingOptions
{
    public TracingIngestionOptions Ingestion { get; set; } = new();
    public TracingStorageOptions Storage { get; set; } = new();
    public TracingQueryOptions Query { get; set; } = new();
    public TracingAlertsOptions Alerts { get; set; } = new();
    public TracingMetricsOptions Metrics { get; set; } = new();
    public TracingServiceMapOptions ServiceMap { get; set; } = new();
    public TracingCollectorsOptions Collectors { get; set; } = new();
    public TracingLiveOptions Live { get; set; } = new();
    public TracingRetentionOptions Retention { get; set; } = new();
}

public sealed class TracingIngestionOptions
{
    public bool Enabled { get; set; } = true;
    public string? Endpoint { get; set; } = "http://localhost:4318";
    public string? ApiKey { get; set; }
    public long MaxPayloadBytes { get; set; } = 32 * 1024 * 1024;
    public List<IngestionFilterRule> Rules { get; set; } = new();
}

public sealed class IngestionFilterRule
{
    public bool Enabled { get; set; } = true;
    public string Field { get; set; } = "ServiceName";
    public string Operator { get; set; } = "Equals";
    public string Value { get; set; } = string.Empty;
    public string Action { get; set; } = "Drop";
}

public sealed class TracingStorageOptions
{
    public string Provider { get; set; } = "SqlServer";
    public string? ConnectionString { get; set; }
}

public sealed class TracingQueryOptions
{
    public int MaxTracesPerRequest { get; set; } = 100;
    public int MaxSpansPerTrace { get; set; } = 5000;
    public int DefaultLookbackSeconds { get; set; } = 3600;
    public int SlowThresholdMs { get; set; } = 1000;
    public int DashboardWindowSeconds { get; set; } = 86400;
    public int DashboardBuckets { get; set; } = 24;
}

public sealed class TracingAlertsOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxAlerts { get; set; } = 10;
    public double ErrorRateDownThreshold { get; set; } = 5.0;
}

public sealed class TracingMetricsOptions
{
    public bool Enabled { get; set; } = true;
    public int DefaultBucketSeconds { get; set; } = 60;
    public int MaxSeriesPoints { get; set; } = 1000;
}

public sealed class TracingServiceMapOptions
{
    public int WindowSeconds { get; set; } = 86400;
}

public sealed class TracingCollectorsOptions
{
    public int WindowSeconds { get; set; } = 3600;
    public int StaleMinutes { get; set; } = 5;
}

public sealed class TracingLiveOptions
{
    public int StreamIntervalSeconds { get; set; } = 5;
}

public sealed class TracingRetentionOptions
{
    public bool Enabled { get; set; }
    public int RetentionDays { get; set; } = 7;
    public int CleanupIntervalHours { get; set; } = 24;
}
