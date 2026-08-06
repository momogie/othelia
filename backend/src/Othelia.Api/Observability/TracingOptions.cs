namespace Othelia.Api.Observability;

public sealed class TracingOptions
{
    public TracingIngestionOptions Ingestion { get; set; } = new();
    public TracingStorageOptions Storage { get; set; } = new();
    public TracingQueryOptions Query { get; set; } = new();
}

public sealed class TracingIngestionOptions
{
    public bool Enabled { get; set; } = true;
    public string? Endpoint { get; set; } = "http://localhost:4318";
}

public sealed class TracingStorageOptions
{
    public string Provider { get; set; } = "SqlServer";
    public string? ConnectionString { get; set; }
}

public sealed class TracingQueryOptions
{
    public int MaxTracesPerRequest { get; set; } = 100;
    public int DefaultLookbackSeconds { get; set; } = 3600;
}
