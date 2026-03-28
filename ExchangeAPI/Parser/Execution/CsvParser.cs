using System.Text;

namespace ExchangeAPI.Parser.Execution;

public static class CsvParser
{
    public static List<Dictionary<string, object?>> Parse(string content, char delimiter)
    {
        var rows = new List<Dictionary<string, object?>>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
        {
            return rows;
        }

        var headers = SplitLine(lines[0].TrimEnd('\r'), delimiter);

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitLine(line, delimiter);
            var row = new Dictionary<string, object?>();

            for (var j = 0; j < headers.Length; j++)
            {
                row[headers[j]] = j < fields.Length ? (object?)fields[j] : null;
            }

            rows.Add(row);
        }

        return rows;
    }

    // Supports RFC 4180 quoted fields (handles "" as escaped quote inside quotes)
    private static string[] SplitLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
