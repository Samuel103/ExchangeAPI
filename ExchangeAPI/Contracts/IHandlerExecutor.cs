using ExchangeAPI.Models.Responses;
using ExchangeAPI.Parser;

namespace ExchangeAPI.Contracts;

public interface IHandlerExecutor
{
    Task<HandlerResponse> ExecuteAsync(IEnumerable<IHandlerStep> steps, CancellationToken cancellationToken = default);
}