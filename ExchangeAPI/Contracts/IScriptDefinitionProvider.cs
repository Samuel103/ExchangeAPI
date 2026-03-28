using ExchangeAPI.Models;

namespace ExchangeAPI.Contracts;

public interface IScriptDefinitionProvider
{
    IReadOnlyList<DynamicScript> GetScripts();
}