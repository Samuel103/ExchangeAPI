using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class TryCatchStep : IHandlerStep
{
    public IReadOnlyList<IHandlerStep> TrySteps { get; set; } = [];
    public IReadOnlyList<IHandlerStep> CatchSteps { get; set; } = [];
    public string ErrorVariable { get; set; } = "error";

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var step in TrySteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, cancellationToken);

                if (context.HasReturned)
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            context.SetVariable(ErrorVariable, new Dictionary<string, object?>
            {
                ["message"] = ex.Message,
                ["type"] = ex.GetType().Name
            });

            foreach (var step in CatchSteps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, cancellationToken);

                if (context.HasReturned)
                {
                    return;
                }
            }
        }
    }
}