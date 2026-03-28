using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class DeclareVariableStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = "string";
    public string? InitialValueTemplate { get; set; }

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        context.DeclareVariable(Name, TypeName);

        if (!string.IsNullOrWhiteSpace(InitialValueTemplate))
        {
            var value = VariableInterpolator.Interpolate(InitialValueTemplate, context.Variables);
            context.SetVariable(Name, value);
        }

        return Task.CompletedTask;
    }
}
