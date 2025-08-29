namespace DocumentVerificationAPI.Services
{
    public interface ICacheService
    {
        /// <summary>
        /// Gets a cached value by key
        /// </summary>
        Task<T?> GetAsync<T>(string key) where T : class;
        
        /// <summary>
        /// Sets a value in cache with expiration
        /// </summary>
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        
        /// <summary>
        /// Removes a value from cache
        /// </summary>
        Task RemoveAsync(string key);
        
        /// <summary>
        /// Gets or sets a cached value using a factory function
        /// </summary>
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        
        /// <summary>
        /// Checks if a key exists in cache
        /// </summary>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// Clears all cached values (use with caution)
        /// </summary>
        Task ClearAsync();
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        CacheStatistics GetStatistics();
    }

    public class CacheStatistics
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long SetCount { get; set; }
        public long RemoveCount { get; set; }
        public double HitRatio => (HitCount + MissCount) > 0 ? (double)HitCount / (HitCount + MissCount) * 100 : 0;
        public DateTime LastAccessed { get; set; }
        public int ApproximateItemCount { get; set; }
    }
}