using ExchangeAPI.Models;

namespace ExchangeAPI.Contracts;

public interface IEndpointDefinitionProvider
{
    IReadOnlyList<DynamicEndpoint> GetEndpoints();
}