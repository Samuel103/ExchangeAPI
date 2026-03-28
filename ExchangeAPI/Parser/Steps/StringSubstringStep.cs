using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class StringSubstringStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public int Start { get; set; }
    public int? Length { get; set; }

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var source = context.GetVariable(From)?.ToString() ?? string.Empty;

        if (Start < 0 || Start > source.Length)
        {
            throw new InvalidOperationException($"StringSubstring invalid Start={Start} for variable '{From}'.");
        }

        var result = Length.HasValue
            ? source.Substring(Start, Math.Min(Length.Value, source.Length - Start))
            : source.Substring(Start);

        context.SetVariable(Name, result);
        return Task.CompletedTask;
    }
}
