using System;
using System.IO;
using WebSearchServerTasks.Cache;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Watcher
{
   public class TextFileWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Logger _logger = Logger.Instance;
        private readonly SearchCache _cache;

        public TextFileWatcher(string textFilesPath, SearchCache cache)
        {
            _cache = cache;
            _watcher = new FileSystemWatcher(textFilesPath);
            _watcher.Filter = "*.txt";
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Error += OnFileError;
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
            _logger.Info($"[TextFileWatcher] Pratim folder: {_watcher.Path}");
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
            _logger.Info($"[TextFileWatcher] Pracenje foldera zaustavljeno.");
        }
        
        
        private void OnFileError(object sender, ErrorEventArgs e)
        {
            _logger.Info($"[TextFileWatcher] Greska: {e.GetException().Message}");
        }
        
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _logger.Watch($"{e.ChangeType}: {e.Name}");
            _cache.Clear();
            _logger.Watch("Kes obrisan zbog promene fajlova.");
        }
        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
