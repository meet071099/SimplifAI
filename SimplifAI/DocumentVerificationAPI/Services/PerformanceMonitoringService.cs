using System.Collections.Concurrent;
using System.Diagnostics;

namespace DocumentVerificationAPI.Services
{
    public class PerformanceMonitoringService : IPerformanceMonitoringService
    {
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly ConcurrentDictionary<string, PerformanceStats> _operationStats;
        private readonly object _lockObject = new();

        public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
        {
            _logger = logger;
            _operationStats = new ConcurrentDictionary<string, PerformanceStats>();
        }

        public IDisposable StartTimer(string operationName, Dictionary<string, object>? properties = null)
        {
            _logger.LogDebug("Starting timer for operation: {OperationName}", operationName);
            return new OperationTimer(operationName, this, properties);
        }

        public void RecordDuration(string operationName, TimeSpan duration, Dictionary<string, object>? properties = null)
        {
            var stats = _operationStats.AddOrUpdate(operationName, 
                new PerformanceStats { OperationName = operationName },
                (key, existing) => existing);

            lock (_lockObject)
            {
                stats.TotalCalls++;
                stats.TotalDuration = stats.TotalDuration.Add(duration);
                stats.LastCalled = DateTime.UtcNow;

                if (duration < stats.MinDuration)
                    stats.MinDuration = duration;

                if (duration > stats.MaxDuration)
                    stats.MaxDuration = duration;
            }

            // Log performance metrics
            var propertiesStr = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "";
            _logger.LogInformation("Operation {OperationName} completed in {Duration}ms. Properties: {Properties}", 
                operationName, duration.TotalMilliseconds, propertiesStr);

            // Log slow operations
            if (duration.TotalSeconds > 5)
            {
                _logger.LogWarning("Slow operation detected: {OperationName} took {Duration}ms", 
                    operationName, duration.TotalMilliseconds);
            }
        }

        public void RecordCounter(string metricName, long value = 1, Dictionary<string, object>? properties = null)
        {
            var propertiesStr = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "";
            _logger.LogInformation("Counter {MetricName}: {Value}. Properties: {Properties}", 
                metricName, value, propertiesStr);
        }

        public void RecordGauge(string metricName, double value, Dictionary<string, object>? properties = null)
        {
            var propertiesStr = properties != null ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")) : "";
            _logger.LogInformation("Gauge {MetricName}: {Value}. Properties: {Properties}", 
                metricName, value, propertiesStr);
        }

        public PerformanceStats GetOperationStats(string operationName)
        {
            return _operationStats.TryGetValue(operationName, out var stats) 
                ? stats 
                : new PerformanceStats { OperationName = operationName };
        }

        public Dictionary<string, PerformanceStats> GetAllStats()
        {
            return new Dictionary<string, PerformanceStats>(_operationStats);
        }

        public void RecordError(string operationName)
        {
            var stats = _operationStats.AddOrUpdate(operationName, 
                new PerformanceStats { OperationName = operationName },
                (key, existing) => existing);

            lock (_lockObject)
            {
                stats.ErrorCount++;
            }

            _logger.LogWarning("Error recorded for operation: {OperationName}. Total errors: {ErrorCount}", 
                operationName, stats.ErrorCount);
        }
    }
}