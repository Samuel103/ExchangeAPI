using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class SetStep : IHandlerStep
{
    public string Variable { get; set; } = string.Empty;
    public string? ValueTemplate { get; set; }

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var value = VariableInterpolator.Interpolate(ValueTemplate, context.Variables);
        context.SetVariable(Variable, value);
        return Task.CompletedTask;
    }
}
