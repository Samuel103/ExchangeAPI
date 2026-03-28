using ExchangeAPI.Parser;

namespace ExchangeAPI.Contracts;

public interface IHandlerParser
{
    IReadOnlyList<IHandlerStep> Parse(string path);
}