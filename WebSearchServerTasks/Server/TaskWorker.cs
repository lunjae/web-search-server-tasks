using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebSearchServerTasks.Cache;
using WebSearchServerTasks.Logging;
using WebSearchServerTasks.Response;
using WebSearchServerTasks.Search;

namespace WebSearchServerTasks.Server
{
    public class TaskWorker
    {
        private readonly FileSearcher _fileSearcher;
        private readonly SearchCache _cache;
        private readonly ResponseBuilder _responseBuilder;
        private readonly Logger _logger = Logger.Instance;
        private readonly SemaphoreSlim _concurrencySemaphore;


        public TaskWorker(FileSearcher fileSearcher, SearchCache cache, ResponseBuilder responseBuilder,
            int maxConcurrent = 10)
        {
            _fileSearcher = fileSearcher;
            _cache = cache;
            _responseBuilder = responseBuilder;
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        public async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            await _concurrencySemaphore.WaitAsync(ct);

            try
            {
                string rawUrl = context.Request.Url.AbsolutePath.TrimStart('/');
                List<string> keywords = new List<string>(rawUrl.Split('&'));
                string cacheKey = string.Join("&", keywords);

                _logger.Info($"Obrada zahteva - ključne reči: {cacheKey}");

                Dictionary<string, Dictionary<string, int>> results = _cache.Get(cacheKey);

                if (results == null)
                {
                    Task<Dictionary<string, Dictionary<string, int>>> waitTask = _cache.WaitForResultAsync(cacheKey);

                    if (waitTask != null)
                    {
                        results = await waitTask;
                    }
                    else
                    {
                        try
                        {
                            results = await _fileSearcher.SearchAsync(keywords, ct);
                            _cache.Set(cacheKey, results);
                            _cache.CompleteResult(cacheKey, results);
                        }
                        catch (Exception ex)
                        {
                            _cache.FailResult(cacheKey, ex);
                            throw;
                        }
                    }
                }

                var capturedResults = results;
                await Task.FromResult(capturedResults)
                    .ContinueWith(searchTask => { return _responseBuilder.BuildHtml(searchTask.Result, keywords); }, ct,
                        TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default)
                    .ContinueWith(htmlTask =>
                    {
                        SendResponse(context, htmlTask.Result);
                        _logger.Info($"Zahtev završen: {cacheKey}");
                    }, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("Zahtev otkazan zbog gašenja servera.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Greška pri obradi zahteva: {ex.Message}");
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
        private void SendResponse(HttpListenerContext context, string html)//Helper metoda
        {
            try
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                _logger.Error($"Greška pri slanju odgovora: {ex.Message}");
            }
        }
    }
}