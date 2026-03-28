using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class CsvAppendStep : IHandlerStep
{
    public string Source { get; set; } = string.Empty;

    /// <summary>Name of the context variable holding the row (Dictionary) to append.</summary>
    public string RowVariable { get; set; } = string.Empty;

    public string Delimiter { get; set; } = ",";

    private readonly string _filePath;

    public CsvAppendStep(string filePath)
    {
        _filePath = filePath;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var row = context.GetVariable(RowVariable);

        if (row is not Dictionary<string, object?> dict || dict.Count == 0)
        {
            return;
        }

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var fileExists = File.Exists(_filePath);

        await using var writer = new StreamWriter(_filePath, append: true);

        if (!fileExists)
        {
            await writer.WriteLineAsync(string.Join(Delimiter, dict.Keys.Select(k => EscapeField(k))));
        }

        await writer.WriteLineAsync(string.Join(Delimiter, dict.Values.Select(v => EscapeField(v?.ToString() ?? string.Empty))));
    }

    private string EscapeField(string value)
    {
        if (value.Contains(Delimiter) || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
