using ExchangeAPI.Parser.Execution;
using System.Xml.Linq;

namespace ExchangeAPI.Parser;

public class SetStep : IHandlerStep
{
    public string Name { get; set; } = string.Empty;
    public string? ValueTemplate { get; set; }
    public XElement? ValueExpression { get; set; }

    public Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var value = ValueExpression is not null
            ? ValueExpressionEvaluator.Evaluate(ValueExpression, context)
            : VariableInterpolator.Interpolate(ValueTemplate, context.Variables);

        context.SetVariable(Name, value);
        return Task.CompletedTask;
    }
}
