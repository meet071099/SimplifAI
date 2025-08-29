using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace DocumentVerificationAPI.Services
{
    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly CacheStatistics _statistics;
        private readonly object _statsLock = new();

        public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _statistics = new CacheStatistics();
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var result = _memoryCache.Get<T>(key);
                
                lock (_statsLock)
                {
                    if (result != null)
                    {
                        _statistics.HitCount++;
                        _logger.LogDebug("Cache hit for key: {Key}", key);
                    }
                    else
                    {
                        _statistics.MissCount++;
                        _logger.LogDebug("Cache miss for key: {Key}", key);
                    }
                    _statistics.LastAccessed = DateTime.UtcNow;
                }

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached value for key: {Key}", key);
                return Task.FromResult<T?>(null);
            }
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var options = new MemoryCacheEntryOptions();
                
                if (expiration.HasValue)
                {
                    options.AbsoluteExpirationRelativeToNow = expiration.Value;
                }
                else
                {
                    // Default expiration of 30 minutes
                    options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                }

                // Set priority based on expiration time
                options.Priority = expiration?.TotalMinutes > 60 ? CacheItemPriority.High : CacheItemPriority.Normal;

                _memoryCache.Set(key, value, options);

                lock (_statsLock)
                {
                    _statistics.SetCount++;
                    _statistics.LastAccessed = DateTime.UtcNow;
                }

                _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached value for key: {Key}", key);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);

                lock (_statsLock)
                {
                    _statistics.RemoveCount++;
                    _statistics.LastAccessed = DateTime.UtcNow;
                }

                _logger.LogDebug("Removed cached value for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached value for key: {Key}", key);
            }

            return Task.CompletedTask;
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            try
            {
                var value = await factory();
                if (value != null)
                {
                    await SetAsync(key, value, expiration);
                }
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync factory for key: {Key}", key);
                throw;
            }
        }

        public Task<bool> ExistsAsync(string key)
        {
            try
            {
                var exists = _memoryCache.TryGetValue(key, out _);
                _logger.LogDebug("Cache existence check for key: {Key} - {Exists}", key, exists);
                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
                return Task.FromResult(false);
            }
        }

        public Task ClearAsync()
        {
            try
            {
                // Memory cache doesn't have a clear all method, so we'll need to track keys
                // For now, we'll just log this operation
                _logger.LogWarning("Cache clear requested - MemoryCache doesn't support clearing all entries");
                
                // Reset statistics
                lock (_statsLock)
                {
                    _statistics.HitCount = 0;
                    _statistics.MissCount = 0;
                    _statistics.SetCount = 0;
                    _statistics.RemoveCount = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache");
            }

            return Task.CompletedTask;
        }

        public CacheStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new CacheStatistics
                {
                    HitCount = _statistics.HitCount,
                    MissCount = _statistics.MissCount,
                    SetCount = _statistics.SetCount,
                    RemoveCount = _statistics.RemoveCount,
                    LastAccessed = _statistics.LastAccessed,
                    ApproximateItemCount = 0 // MemoryCache doesn't provide item count
                };
            }
        }
    }
}