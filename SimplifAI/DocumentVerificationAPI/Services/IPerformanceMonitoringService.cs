using System.Diagnostics;

namespace DocumentVerificationAPI.Services
{
    public interface IPerformanceMonitoringService
    {
        /// <summary>
        /// Starts timing an operation and returns a disposable timer
        /// </summary>
        IDisposable StartTimer(string operationName, Dictionary<string, object>? properties = null);
        
        /// <summary>
        /// Records the duration of an operation
        /// </summary>
        void RecordDuration(string operationName, TimeSpan duration, Dictionary<string, object>? properties = null);
        
        /// <summary>
        /// Records a counter metric
        /// </summary>
        void RecordCounter(string metricName, long value = 1, Dictionary<string, object>? properties = null);
        
        /// <summary>
        /// Records a gauge metric
        /// </summary>
        void RecordGauge(string metricName, double value, Dictionary<string, object>? properties = null);
        
        /// <summary>
        /// Gets performance statistics for an operation
        /// </summary>
        PerformanceStats GetOperationStats(string operationName);
        
        /// <summary>
        /// Gets all recorded performance statistics
        /// </summary>
        Dictionary<string, PerformanceStats> GetAllStats();
    }

    public class PerformanceStats
    {
        public string OperationName { get; set; } = string.Empty;
        public long TotalCalls { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageDuration => TotalCalls > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalCalls) : TimeSpan.Zero;
        public TimeSpan MinDuration { get; set; } = TimeSpan.MaxValue;
        public TimeSpan MaxDuration { get; set; } = TimeSpan.MinValue;
        public DateTime LastCalled { get; set; }
        public long ErrorCount { get; set; }
        public double SuccessRate => TotalCalls > 0 ? (double)(TotalCalls - ErrorCount) / TotalCalls * 100 : 0;
    }

    public class OperationTimer : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly string _operationName;
        private readonly Dictionary<string, object>? _properties;
        private readonly IPerformanceMonitoringService _monitoringService;
        private bool _disposed = false;

        public OperationTimer(string operationName, IPerformanceMonitoringService monitoringService, Dictionary<string, object>? properties = null)
        {
            _operationName = operationName;
            _monitoringService = monitoringService;
            _properties = properties;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _monitoringService.RecordDuration(_operationName, _stopwatch.Elapsed, _properties);
                _disposed = true;
            }
        }
    }
}