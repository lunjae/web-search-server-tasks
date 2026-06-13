using System.Collections.Generic;
using System.Threading.Tasks;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Cache
{
    public class SearchCache
    {
        private readonly Dictionary<string, CacheEntry> _cache;
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
            _inProgress =
                new Dictionary<string, TaskCompletionSource<Dictionary<string, Dictionary<string, int>>>>();
        }

        // Atomična operacija — Get + WaitForResult u jednom lock bloku
        // Vraća:
        // (rezultat, null)  — HIT, odmah nastavljamo
        // (null, task)      — u toku, čekamo
        // (null, null)      — MISS, mi pretražujemo
        public (Dictionary<string, Dictionary<string, int>> result,
            Task<Dictionary<string, Dictionary<string, int>>> waitTask) GetOrRegister(string key)
        {
            lock (_lock)
            {
                // 1. Provjeri keš
                if (_cache.ContainsKey(key))
                {
                    CacheEntry entry = _cache[key];
                    _lruOrder.Remove(entry.LruNode);
                    _lruOrder.AddFirst(entry.LruNode);
                    _logger.Cache($"HIT - {key}");
                    return (entry.Results, null);
                }

                // 2. Provjeri da li je već u toku
                if (_inProgress.ContainsKey(key))
                {
                    _logger.Cache($"WAIT - {key}");
                    return (null, _inProgress[key].Task);
                }

                // 3. Prva nit — registruj i idi na pretragu
                _logger.Cache($"MISS - {key}");
                var tcs = new TaskCompletionSource<Dictionary<string, Dictionary<string, int>>>();
                _inProgress[key] = tcs;
                return (null, null);
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