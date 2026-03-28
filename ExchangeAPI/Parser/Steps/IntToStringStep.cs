using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class IntToStringStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var value = context.GetVariable(From);
        context.SetVariable(Name, value?.ToString() ?? string.Empty);
        return Task.CompletedTask;
    }
}
