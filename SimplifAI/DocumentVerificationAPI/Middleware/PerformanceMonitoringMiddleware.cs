using DocumentVerificationAPI.Services;
using System.Diagnostics;

namespace DocumentVerificationAPI.Middleware
{
    public class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private readonly IPerformanceMonitoringService _performanceMonitoring;

        public PerformanceMonitoringMiddleware(
            RequestDelegate next, 
            ILogger<PerformanceMonitoringMiddleware> logger,
            IPerformanceMonitoringService performanceMonitoring)
        {
            _next = next;
            _logger = logger;
            _performanceMonitoring = performanceMonitoring;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestPath = context.Request.Path.Value ?? "unknown";
            var method = context.Request.Method;
            var operationName = $"{method} {requestPath}";

            // Add request properties for monitoring
            var properties = new Dictionary<string, object>
            {
                ["Method"] = method,
                ["Path"] = requestPath,
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };

            try
            {
                // Log request start
                _logger.LogInformation("Request started: {Method} {Path} from {RemoteIpAddress}", 
                    method, requestPath, context.Connection.RemoteIpAddress);

                await _next(context);

                stopwatch.Stop();

                // Add response properties
                properties["StatusCode"] = context.Response.StatusCode;
                properties["Success"] = context.Response.StatusCode < 400;

                // Record performance metrics
                _performanceMonitoring.RecordDuration(operationName, stopwatch.Elapsed, properties);
                _performanceMonitoring.RecordCounter("http_requests_total", 1, properties);

                // Log request completion
                _logger.LogInformation("Request completed: {Method} {Path} - {StatusCode} in {Duration}ms", 
                    method, requestPath, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);

                // Log slow requests
                if (stopwatch.ElapsedMilliseconds > 2000)
                {
                    _logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}ms", 
                        method, requestPath, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Record error metrics
                properties["StatusCode"] = 500;
                properties["Success"] = false;
                properties["ErrorType"] = ex.GetType().Name;

                _performanceMonitoring.RecordDuration(operationName, stopwatch.Elapsed, properties);
                _performanceMonitoring.RecordCounter("http_requests_errors_total", 1, properties);

                _logger.LogError(ex, "Request failed: {Method} {Path} in {Duration}ms", 
                    method, requestPath, stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
    }

    public static class PerformanceMonitoringMiddlewareExtensions
    {
        public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
        }
    }
}