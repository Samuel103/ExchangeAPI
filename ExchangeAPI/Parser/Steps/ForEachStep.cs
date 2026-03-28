using ExchangeAPI.Parser.Execution;
using System.Collections;
using System.Xml.Linq;

namespace ExchangeAPI.Parser;

public class ForEachStep : IHandlerStep
{
    public string ItemVariable { get; set; } = "item";
    public string? IndexVariable { get; set; }
    public XElement? CollectionExpression { get; set; }
    public string? CollectionTemplate { get; set; }
    public IReadOnlyList<IHandlerStep> BodySteps { get; set; } = [];

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var collectionValue = CollectionExpression is not null
            ? ValueExpressionEvaluator.Evaluate(CollectionExpression, context)
            : VariableInterpolator.Interpolate(CollectionTemplate, context.Variables);

        var items = ResolveItems(collectionValue);
        var index = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            context.SetVariable(ItemVariable, item);
            if (!string.IsNullOrWhiteSpace(IndexVariable))
            {
                context.SetVariable(IndexVariable!, index);
            }

            foreach (var step in BodySteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, cancellationToken);

                if (context.HasReturned)
                {
                    return;
                }
            }

            index++;
        }
    }

    private static IEnumerable<object?> ResolveItems(object? collectionValue)
    {
        if (collectionValue is null)
        {
            return [];
        }

        if (collectionValue is string)
        {
            return [];
        }

        if (collectionValue is IEnumerable<object?> typedEnumerable)
        {
            return typedEnumerable;
        }

        if (collectionValue is IEnumerable enumerable)
        {
            return enumerable.Cast<object?>();
        }

        return [collectionValue];
    }
}