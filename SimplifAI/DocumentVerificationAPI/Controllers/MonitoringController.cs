using DocumentVerificationAPI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DocumentVerificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController : ControllerBase
    {
        private readonly IPerformanceMonitoringService _performanceMonitoring;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MonitoringController> _logger;

        public MonitoringController(
            IPerformanceMonitoringService performanceMonitoring,
            ICacheService cacheService,
            ILogger<MonitoringController> logger)
        {
            _performanceMonitoring = performanceMonitoring;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Gets performance statistics for all operations
        /// </summary>
        [HttpGet("performance")]
        public ActionResult<Dictionary<string, PerformanceStats>> GetPerformanceStats()
        {
            try
            {
                var stats = _performanceMonitoring.GetAllStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving performance statistics");
                return StatusCode(500, "Error retrieving performance statistics");
            }
        }

        /// <summary>
        /// Gets performance statistics for a specific operation
        /// </summary>
        [HttpGet("performance/{operationName}")]
        public ActionResult<PerformanceStats> GetOperationStats(string operationName)
        {
            try
            {
                var stats = _performanceMonitoring.GetOperationStats(operationName);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving performance statistics for operation {OperationName}", operationName);
                return StatusCode(500, "Error retrieving performance statistics");
            }
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        [HttpGet("cache")]
        public ActionResult<CacheStatistics> GetCacheStats()
        {
            try
            {
                var stats = _cacheService.GetStatistics();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache statistics");
                return StatusCode(500, "Error retrieving cache statistics");
            }
        }

        /// <summary>
        /// Gets system health information
        /// </summary>
        [HttpGet("health")]
        public ActionResult<SystemHealthInfo> GetSystemHealth()
        {
            try
            {
                var healthInfo = new SystemHealthInfo
                {
                    Timestamp = DateTime.UtcNow,
                    Status = "Healthy",
                    Version = GetType().Assembly.GetName().Version?.ToString() ?? "Unknown",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    MachineName = Environment.MachineName,
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    UpTime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime
                };

                return Ok(healthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system health information");
                return StatusCode(500, "Error retrieving system health information");
            }
        }

        /// <summary>
        /// Clears cache (use with caution)
        /// </summary>
        [HttpPost("cache/clear")]
        public async Task<ActionResult> ClearCache()
        {
            try
            {
                await _cacheService.ClearAsync();
                _logger.LogWarning("Cache cleared via API request from {RemoteIpAddress}", 
                    HttpContext.Connection.RemoteIpAddress);
                return Ok(new { message = "Cache cleared successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
                return StatusCode(500, "Error clearing cache");
            }
        }

        /// <summary>
        /// Gets top slow operations
        /// </summary>
        [HttpGet("performance/slow")]
        public ActionResult<List<SlowOperationInfo>> GetSlowOperations([FromQuery] int limit = 10)
        {
            try
            {
                var allStats = _performanceMonitoring.GetAllStats();
                var slowOperations = allStats.Values
                    .Where(s => s.TotalCalls > 0)
                    .OrderByDescending(s => s.AverageDuration.TotalMilliseconds)
                    .Take(limit)
                    .Select(s => new SlowOperationInfo
                    {
                        OperationName = s.OperationName,
                        AverageDuration = s.AverageDuration,
                        MaxDuration = s.MaxDuration,
                        TotalCalls = s.TotalCalls,
                        ErrorCount = s.ErrorCount,
                        SuccessRate = s.SuccessRate
                    })
                    .ToList();

                return Ok(slowOperations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving slow operations");
                return StatusCode(500, "Error retrieving slow operations");
            }
        }
    }

    public class SystemHealthInfo
    {
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public TimeSpan UpTime { get; set; }
    }

    public class SlowOperationInfo
    {
        public string OperationName { get; set; } = string.Empty;
        public TimeSpan AverageDuration { get; set; }
        public TimeSpan MaxDuration { get; set; }
        public long TotalCalls { get; set; }
        public long ErrorCount { get; set; }
        public double SuccessRate { get; set; }
    }
}