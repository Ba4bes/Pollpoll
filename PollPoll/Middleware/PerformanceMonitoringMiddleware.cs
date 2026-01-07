using System.Diagnostics;

namespace PollPoll.Middleware;

/// <summary>
/// Middleware to log request durations and performance metrics (T107)
/// Helps validate PERF-001 through PERF-010 requirements
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path;
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Log slow requests (>500ms for reads, >300ms for writes per PERF requirements)
            var threshold = method == "GET" ? 500 : 300;
            var logLevel = elapsed > threshold ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                method, path, statusCode, elapsed);

            // Track specific performance-critical endpoints
            if (path.StartsWithSegments("/host/polls") && method == "POST")
            {
                // PERF-001: Poll creation <500ms
                if (elapsed > 500)
                {
                    _logger.LogWarning(
                        "PERF-001 violation: Poll creation took {ElapsedMs}ms (target: <500ms)",
                        elapsed);
                }
            }
            else if (path.StartsWithSegments("/p") && path.Value?.Contains("/vote") != true && method == "POST")
            {
                // PERF-002: Vote submission <300ms p95
                if (elapsed > 300)
                {
                    _logger.LogWarning(
                        "PERF-002: Vote submission took {ElapsedMs}ms (target: <300ms p95)",
                        elapsed);
                }
            }
            else if (path.StartsWithSegments("/p") && path.Value?.Contains("/results") == true && method == "GET")
            {
                // PERF-003: Results page load <2s
                if (elapsed > 2000)
                {
                    _logger.LogWarning(
                        "PERF-003 violation: Results page load took {ElapsedMs}ms (target: <2000ms)",
                        elapsed);
                }
            }
            else if (path.StartsWithSegments("/api/results") && method == "GET")
            {
                // PERF-005: Results API <500ms
                if (elapsed > 500)
                {
                    _logger.LogWarning(
                        "PERF-005: Results API took {ElapsedMs}ms (target: <500ms)",
                        elapsed);
                }
            }
        }
    }
}

/// <summary>
/// Extension methods for registering performance monitoring middleware
/// </summary>
public static class PerformanceMonitoringMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
    }
}
