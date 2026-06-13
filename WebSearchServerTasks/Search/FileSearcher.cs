using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebSearchServerTasks.Logging;

namespace WebSearchServerTasks.Search
{
    public class FileSearcher
{
    // cuva putanju do fajlova
    private readonly string _textFilesPath;
    // uzima instancu loggera kroz singleton obrazac
    private readonly Logger _logger = Logger.Instance;

    public FileSearcher(string textFilesPath)
    {
        _textFilesPath = textFilesPath;
    }

    public async Task<Dictionary<string, Dictionary<string, int>>> SearchAsync(List<string> keywords, CancellationToken token)
    {
        // Prazan dictionary koji popunjavamo rezultatima
        var results = new Dictionary<string, Dictionary<string, int>>();
        
        // Cita fajlove koji se zavrsavaju sa .txt iz TextFiles/
        string[] files = Directory.GetFiles(_textFilesPath, "*.txt");
        
        //Loger za info koliko je pronadjeno
        _logger.Info($"[FileSearcher] Pronadjeno {files.Length} fajlova u {_textFilesPath}");
        
        foreach(string filePath in files)
        {
            // Uzima ime fajla
            string fileName = Path.GetFileName(filePath);
            // sadrzaj fajla
            string content;
            try
            {
                content = await File.ReadAllTextAsync(filePath);
                content = content.ToLower();
            }
            catch (IOException e)
            {
                _logger.Error($"[FileSearcher] Greska pri citanju {fileName}: {e.Message}");
                continue;
            }
            
            var wordCounts = new Dictionary<string, int>();
            foreach (string keyword in keywords)
            {
                string kw = keyword.ToLower().Trim();
                if (string.IsNullOrEmpty(kw)) continue;
                int count = CountOccurrences(content, kw);
                wordCounts[kw] = count;
            }
            results[fileName] = wordCounts;
            _logger.Info($"[FileSearcher] File: {fileName}, obradjen");
        }
            return results;
    }

    private static int CountOccurrences(string content, string kw)
    {
        int count = 0;
        int index = 0;

        while ((index = content.IndexOf(kw, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += kw.Length;
        }

        return count;
    }
    
}
    
}

