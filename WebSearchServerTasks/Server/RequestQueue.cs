using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace WebSearchServerTasks.Server
{
    public class RequestQueue
    {
        private readonly Queue<HttpListenerContext> _queue;
        private readonly SemaphoreSlim _enqueSemaphore;
        private readonly SemaphoreSlim _dequeSemaphore;
        private readonly object _lock = new object();
        private readonly int _maxSize;
        
        public RequestQueue(int maxSize =100)
        {
            _queue = new Queue<HttpListenerContext>();
            _maxSize = maxSize;
            _enqueSemaphore = new SemaphoreSlim(maxSize, maxSize);
            _dequeSemaphore = new SemaphoreSlim(0, maxSize);
        }

        public async Task EnqueueAsync(HttpListenerContext context, CancellationToken ct = default)
        {
            await _enqueSemaphore.WaitAsync(ct);

            lock (_lock)
            {
                _queue.Enqueue(context);
            }
            _dequeSemaphore.Release();
        }
        
        public async Task<HttpListenerContext> DequeueAsync(CancellationToken ct = default)
        {
            await _dequeSemaphore.WaitAsync(ct);
            
            HttpListenerContext context;
            lock (_lock)
            {
                context = _queue.Dequeue();
            }
            _enqueSemaphore.Release();
            return context;
        }
        
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }
    }
}