using System.Collections.Generic;
using System.Text;

namespace WebSearchServerTasks.Response
{
    public class ResponseBuilder
    {
        public string BuildHtml(Dictionary<string, Dictionary<string, int>> searchResults, List<string> keywords)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='sr'>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset='UTF-8'>");
            sb.AppendLine("  <title>Rezultati pretrage</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: Arial, sans-serif; margin: 40px; }");
            sb.AppendLine("    table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("    th, td { border: 1px solid #ccc; padding: 8px 12px; text-align: center; }");
            sb.AppendLine("    th { background-color: #4a90d9; color: white; }");
            sb.AppendLine("    tr:nth-child(even) { background-color: #f2f2f2; }");
            sb.AppendLine("    .zero { color: #aaa; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <h2>Rezultati pretrage</h2>");

            if (searchResults == null || searchResults.Count == 0)
            {
                sb.AppendLine("  <p>Nema pronađenih fajlova.</p>");
                sb.AppendLine("</body></html>");
                return sb.ToString();
            }

            sb.AppendLine("  <table>");

            sb.AppendLine("    <tr>");
            sb.AppendLine("      <th>Fajl</th>");
            foreach (string kw in keywords)
            {
                sb.AppendLine($"      <th>{kw}</th>");
            }
            sb.AppendLine("      <th>Ukupno</th>");
            sb.AppendLine("    </tr>");

            foreach (var entry in searchResults)
            {
                string fileName = entry.Key;
                Dictionary<string, int> wordCounts = entry.Value;

                int total = 0;
                foreach (var wc in wordCounts)
                    total += wc.Value;

                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{fileName}</td>");

                foreach (string kw in keywords)
                {
                    string kwLower = kw.ToLower().Trim();
                    int count = wordCounts.ContainsKey(kwLower) ? wordCounts[kwLower] : 0;
                    string cssClass = count == 0 ? " class='zero'" : "";
                    sb.AppendLine($"      <td{cssClass}>{count}</td>");
                }

                sb.AppendLine($"      <td><strong>{total}</strong></td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("  </table>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}