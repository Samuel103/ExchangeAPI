using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class FileReadStep : IHandlerStep
{
    public string Source { get; set; } = string.Empty;
    public string OutputVariable { get; set; } = string.Empty;

    private readonly string _filePath;

    public FileReadStep(string filePath)
    {
        _filePath = filePath;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(_filePath, cancellationToken);

        if (!string.IsNullOrWhiteSpace(OutputVariable))
        {
            context.SetVariable(OutputVariable, content);
        }
    }
}
