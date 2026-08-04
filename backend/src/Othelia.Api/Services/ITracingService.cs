using Othelia.Api.Models;

namespace Othelia.Api.Services;

public interface ITracingService
{
    Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TraceSummaryDto>> GetTracesAsync(
        string? service = null,
        string? traceId = null,
        int? limit = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SpanDto>> GetSpansAsync(string traceId, CancellationToken ct = default);
}
