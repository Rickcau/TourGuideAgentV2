using System;
using System.Collections.Concurrent;
using System.Threading;
using TourGuideAgentV2.Models;

namespace TourGuideAgentV2.Services
{
    public sealed class JobResultsCacheService
    {
        private static readonly Lazy<JobResultsCacheService> _instance = new Lazy<JobResultsCacheService>(() => new JobResultsCacheService(), LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly ConcurrentDictionary<string, Tuple<JobResults, DateTime>> _cache;
        private readonly Timer _cleanupTimer;

        private JobResultsCacheService()
        {
            _cache = new ConcurrentDictionary<string, Tuple<JobResults, DateTime>>();
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        public static JobResultsCacheService Instance => _instance.Value;

        public void StoreJobResults(string jobId, JobResults results)
        {
            _cache[jobId] = Tuple.Create(results, DateTime.UtcNow.AddHours(1));
        }

        public JobResults? GetJobResults(string jobId)
        {
            if (_cache.TryGetValue(jobId, out var tuple) && tuple.Item2 > DateTime.UtcNow)
            {
                return tuple.Item1;
            }
            return null;
        }

       private void CleanupExpiredEntries(object? state)
        {
            var now = DateTime.UtcNow;
            foreach (var key in _cache.Keys)
            {
                if (_cache.TryGetValue(key, out var tuple) && tuple.Item2 <= now)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }
}