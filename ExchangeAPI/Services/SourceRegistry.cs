using ExchangeAPI.Configuration;
using ExchangeAPI.Contracts;
using Microsoft.Extensions.Options;

namespace ExchangeAPI.Services;

public class SourceRegistry : ISourceRegistry
{
    private readonly SourcesConfiguration _config;
    private readonly string _workingFolder;

    public SourceRegistry(SourcesConfiguration config, IOptions<EnvironmentOptions> options)
    {
        _config = config;
        _workingFolder = options.Value.WorkingFolder;
    }

    public string GetConnectionString(string sourceName)
    {
        if (_config.Sources.TryGetValue(sourceName, out var def) &&
            !string.IsNullOrWhiteSpace(def.ConnectionString))
        {
            return def.ConnectionString;
        }

        throw new InvalidOperationException(
            $"Database source '{sourceName}' not found or has no connection string in configurations.json.");
    }

    public string GetFilePath(string sourceName)
    {
        if (_config.Sources.TryGetValue(sourceName, out var def) &&
            !string.IsNullOrWhiteSpace(def.Path))
        {
            var path = def.Path;
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(_workingFolder, path));
        }

        throw new InvalidOperationException(
            $"File source '{sourceName}' not found or has no path in configurations.json.");
    }
}
