using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class StringToIntStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var value = context.GetVariable(From);
        if (value is null)
        {
            throw new InvalidOperationException($"StringToInt missing source variable '{From}'.");
        }

        if (!int.TryParse(value.ToString(), out var parsed))
        {
            throw new InvalidOperationException($"StringToInt could not parse variable '{From}' value '{value}'.");
        }

        context.SetVariable(Name, parsed);
        return Task.CompletedTask;
    }
}
