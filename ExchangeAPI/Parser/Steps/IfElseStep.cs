using ExchangeAPI.Parser.Execution;
using System.Xml.Linq;

namespace ExchangeAPI.Parser;

public class IfElseStep : IHandlerStep
{
    public XElement? ConditionExpression { get; set; }
    public string? ConditionTemplate { get; set; }
    public IReadOnlyList<IHandlerStep> ThenSteps { get; set; } = [];
    public IReadOnlyList<IHandlerStep> ElseSteps { get; set; } = [];

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var conditionValue = ConditionExpression is not null
            ? ValueExpressionEvaluator.Evaluate(ConditionExpression, context)
            : VariableInterpolator.Interpolate(ConditionTemplate, context.Variables);

        var shouldRunThen = ConvertToBool(conditionValue);
        var steps = shouldRunThen ? ThenSteps : ElseSteps;

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await step.ExecuteAsync(context, cancellationToken);

            if (context.HasReturned)
            {
                break;
            }
        }
    }

    private static bool ConvertToBool(object? value)
    {
        if (value is null)
        {
            return false;
        }

        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsedBool) => parsedBool,
            string s when int.TryParse(s, out var parsedInt) => parsedInt != 0,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > double.Epsilon,
            decimal m => m != 0m,
            _ => true
        };
    }
}