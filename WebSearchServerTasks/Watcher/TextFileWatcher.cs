using System;
using System.IO;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Watcher
{
    class TextFileWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Logger _logger = Logger.Instance;

        public TextFileWatcher(string textFilesPath)
        {
            _watcher = new FileSystemWatcher(textFilesPath);
            _watcher.Filter = "*.txt";
            _watcher.NotifyFilter = NotifyFilters.FileName;
            _watcher.Created += OnFileCreated;
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

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _logger.Info($"[TextFileWatcher] Novi fajl detektovan: {e.Name}");
        }
        
        private void OnFileError(object sender, ErrorEventArgs e)
        {
            _logger.Info($"[TextFileWatcher] Greska: {e.GetException().Message}");
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }
}
