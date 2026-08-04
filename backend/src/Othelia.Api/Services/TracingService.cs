using Othelia.Api.Models;

namespace Othelia.Api.Services;

public sealed class TracingService : ITracingService
{
    public Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ServiceDto>>(Array.Empty<ServiceDto>());

    public Task<IReadOnlyList<TraceSummaryDto>> GetTracesAsync(
        string? service = null,
        string? traceId = null,
        int? limit = null,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TraceSummaryDto>>(Array.Empty<TraceSummaryDto>());

    public Task<IReadOnlyList<SpanDto>> GetSpansAsync(string traceId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SpanDto>>(Array.Empty<SpanDto>());
}
