namespace ExchangeAPI.Contracts;

public interface ISourceRegistry
{
    string GetConnectionString(string sourceName);
    string GetFilePath(string sourceName);
}
