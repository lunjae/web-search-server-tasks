using System;
using System.IO;

namespace WebSearchServerTasks.Logging
{
    public class Logger
    {
        private static readonly Logger _instance = new Logger();
        private readonly object _lock = new object();
        private readonly string _logFilePath;

        private Logger()
        {
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server.log");
        }
        
        public static Logger Instance => _instance;

        public void Log(string level,string message)
        {
            string entry= $"[{DateTime.Now}] [{level}]: {message}";

            lock (_lock)
            {
                Console.WriteLine(entry);
                File.AppendAllText(_logFilePath, entry+ Environment.NewLine);
            }
        }
        
        public void Info(string message)  => Log("INFO ", message);
        public void Warn(string message)  => Log("WARN ", message);
        public void Error(string message) => Log("ERROR", message);
        public void Cache(string message) => Log("CACHE", message);
        public void Watch(string message) => Log("WATCH", message);
    }
}