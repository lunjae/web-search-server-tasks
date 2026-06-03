using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using WebSearchServerTasks.Cache;
using WebSearchServerTasks.Logging;
using WebSearchServerTasks.Response;
using WebSearchServerTasks.Search;
using WebSearchServerTasks.Server;
using WebSearchServerTasks.Watcher;

class Program
{
    static async Task Main(string[] args)
    {
        Logger logger = Logger.Instance;
        
        string textFilesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextFiles");

        if (!Directory.Exists(textFilesPath))
        {
            logger.Error($"TextFiles folder ne postoji: {textFilesPath}");
            return;
        }

        logger.Info($"TextFiles folder: {textFilesPath}");
        
        using CancellationTokenSource cts = new CancellationTokenSource();

        FileSearcher fileSearcher = new FileSearcher(textFilesPath);
        SearchCache cache = new SearchCache(maxSize: 50);
        ResponseBuilder responseBuilder = new ResponseBuilder();
        RequestQueue requestQueue = new RequestQueue(maxSize: 100);
        TaskWorker worker = new TaskWorker(fileSearcher, cache, responseBuilder, maxConcurrent: 10);
        TextFileWatcher watcher = new TextFileWatcher(textFilesPath);
        
        string prefix = "http://localhost:5050/";
        HttpServer server = new HttpServer(prefix, requestQueue);

        Task watcherTask = Task.Run(() => watcher.Start(), cts.Token);
        
        Task serverTask = server.StartAsync(cts.Token);

        Task workerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    HttpListenerContext context = await requestQueue.DequeueAsync(cts.Token);
                    _ = worker.ProcessRequestAsync(context, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        logger.Info("Pritisnite Q za zaustavljanje servera.");
        
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Q)
            {
                logger.Info("Zaustavljanje servera...");
                cts.Cancel();
                break;
            }
        }

        try
        {
            await Task.WhenAll(serverTask, workerTask, watcherTask);
        }
        catch (OperationCanceledException)
        {
            // ocekivano pri gasenju
        }

        logger.Info("Server uspešno zaustavljen.");
    }
}