using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class LogStep : IHandlerStep
{
    public string Message { get; set; } = string.Empty;

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var resolved = VariableInterpolator.InterpolateString(Message, context.Variables);
        Console.WriteLine(resolved);
        return Task.CompletedTask;
    }
}