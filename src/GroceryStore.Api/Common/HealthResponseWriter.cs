using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GroceryStore.Api.Common;

/// <summary>
/// Writes health results as small JSON. Exception messages are deliberately omitted: the endpoint is
/// anonymous and must not leak server or connection details. Failures are logged by the health check service.
/// </summary>
internal static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
        });
}
