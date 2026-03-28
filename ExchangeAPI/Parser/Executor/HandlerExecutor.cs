using ExchangeAPI.Contracts;
using ExchangeAPI.Models.Responses;
using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class HandlerExecutor : IHandlerExecutor
{
    public async Task<HandlerResponse> ExecuteAsync(IEnumerable<IHandlerStep> steps, CancellationToken cancellationToken = default)
    {
        var context = new HandlerExecutionContext();

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await step.ExecuteAsync(context, cancellationToken);

            if (context.HasReturned)
            {
                break;
            }
        }

        return context.Response;
    }
}