using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class FileReadStep : IHandlerStep
{
    public string Source { get; set; } = string.Empty;
    public string OutputVariable { get; set; } = string.Empty;

    /// <summary>
    /// Delimiter character for CSV parsing (e.g. "," or ";").
    /// If set, the file is parsed as CSV and each row becomes a Dictionary.
    /// If not set, auto-detects CSV by .csv extension (defaults to comma); otherwise stores raw text.
    /// </summary>
    public string? Delimiter { get; set; }

    private readonly string _filePath;

    public FileReadStep(string filePath)
    {
        _filePath = filePath;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(_filePath, cancellationToken);

        var isCsv = Delimiter is not null
            || _filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

        object? result = isCsv
            ? CsvParser.Parse(content, string.IsNullOrEmpty(Delimiter) ? ',' : Delimiter[0])
            : content;

        if (!string.IsNullOrWhiteSpace(OutputVariable))
        {
            context.SetVariable(OutputVariable, result);
        }
    }
}
