using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Cache
{
    public class SearchCache
    {
        private readonly Dictionary<string, CacheEntry>_cache;
        private readonly LinkedList<string> _lruOrder;
        private readonly object _lock = new object();
        private readonly int _maxSize;
        private readonly Logger _logger = Logger.Instance;

        private readonly Dictionary<string, TaskCompletionSource<Dictionary<string, Dictionary<string, int>>>>
            _inProgress;
        
        public SearchCache(int maxSize = 50)
        {
            _cache = new Dictionary<string, CacheEntry>();
            _lruOrder = new LinkedList<string>();
            _maxSize = maxSize;
            _inProgress = new Dictionary<string, TaskCompletionSource<Dictionary<string, Dictionary<string, int>>>>();
        }
        
        public Dictionary<string, Dictionary<string, int>> Get(string key)
        {
            lock (_lock)
            {
                if (!_cache.ContainsKey(key))
                {
                    _logger.Cache($"MISS - {key}");
                    return null;
                }

                CacheEntry entry = _cache[key];
                _lruOrder.Remove(entry.LruNode);
                _lruOrder.AddFirst(entry.LruNode);

                _logger.Cache($"HIT - {key}");
                return entry.Results;
            }
        }
        
        public void Set(string key, Dictionary<string, Dictionary<string, int>> results)
        {
            lock (_lock)
            {
                if (_cache.ContainsKey(key))
                {
                    CacheEntry existing = _cache[key];
                    _lruOrder.Remove(existing.LruNode);
                    _lruOrder.AddFirst(key);
                    existing.LruNode = _lruOrder.First;
                    existing.Results = results;
                    return;
                }

                if (_cache.Count >= _maxSize)
                {
                    string oldest = _lruOrder.Last.Value;
                    _lruOrder.RemoveLast();
                    _cache.Remove(oldest);
                    _logger.Cache($"Evicted - {oldest}");
                }

                LinkedListNode<string> node = _lruOrder.AddFirst(key);
                _cache[key] = new CacheEntry { Results = results, LruNode = node };
                _logger.Cache($"SET - {key}");
            }
        }
        
        
        public Task<Dictionary<string, Dictionary<string, int>>> WaitForResultAsync(string key)
        {
            lock (_lock)
            {
                if (_inProgress.ContainsKey(key))
                    return _inProgress[key].Task;
        
                var tcs = new TaskCompletionSource<Dictionary<string, Dictionary<string, int>>>();
                _inProgress[key] = tcs;
                return null;
            }
        }

        public void CompleteResult(string key, Dictionary<string, Dictionary<string, int>> results)
        {
            TaskCompletionSource<Dictionary<string, Dictionary<string, int>>> tcs = null;
    
            lock (_lock)
            {
                if (_inProgress.ContainsKey(key))
                {
                    tcs = _inProgress[key];
                    _inProgress.Remove(key);
                }
            }
    
            tcs?.SetResult(results);
        }

        public void FailResult(string key, Exception ex)
        {
            TaskCompletionSource<Dictionary<string, Dictionary<string, int>>> tcs = null;
    
            lock (_lock)
            {
                if (_inProgress.ContainsKey(key))
                {
                    tcs = _inProgress[key];
                    _inProgress.Remove(key);
                }
            }
    
            tcs?.SetException(ex);
        }
        
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruOrder.Clear();
                _logger.Cache("Kes kompletno obrisan.");
            }
        }
    }
    
    
    public class CacheEntry
    {
        public Dictionary<string, Dictionary<string, int>> Results { get; set; }
        public LinkedListNode<string> LruNode { get; set; }
    }
    
    
}