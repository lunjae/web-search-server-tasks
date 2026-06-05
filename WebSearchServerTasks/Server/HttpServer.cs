using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Server
{
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly RequestQueue _requestQueue;
        private readonly string _prefix;
        private readonly Logger _logger= Logger.Instance;

        public HttpServer(string prefix, RequestQueue requestQueue)
        {
            _prefix = prefix;
            _listener= new HttpListener();
            _listener.Prefixes.Add(prefix);
            _requestQueue = requestQueue;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _listener.Start();
            ct.Register(() => _listener.Stop());
            _logger.Info($"[HttpServer] Server pokrenut na  {_prefix}");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        HttpListenerContext context = await Task.Run(() => _listener.GetContext(), ct);
                        _logger.Info($"[HttpServer] Primljen zahtev: {context.Request.RawUrl}");
                        await _requestQueue.EnqueueAsync(context, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (HttpListenerException e)
                    {
                        if (!ct.IsCancellationRequested)
                            _logger.Error($"[HttpServer] Greska: {e.Message}");
                    }
                }
            }
            finally
            {
                _listener.Stop();
                _logger.Info($"[HttpServer] Server zaustavljen");
            }
        
        }
    }
}