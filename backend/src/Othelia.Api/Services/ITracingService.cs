using Othelia.Api.Models;
using Othelia.Api.Observability;

namespace Othelia.Api.Services;

public interface ITracingService
{
    Task<IReadOnlyList<ServiceDto>> GetServicesAsync(CancellationToken ct = default);

    Task<TraceQueryResult> GetTracesAsync(
        TraceQuery query,
        CancellationToken ct = default);

    Task<IReadOnlyList<SpanDto>> GetSpansAsync(string traceId, CancellationToken ct = default);
}
