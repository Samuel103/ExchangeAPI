namespace ExchangeAPI.Configuration;

public class SourcesConfiguration
{
    public Dictionary<string, SourceDefinition> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class SourceDefinition
{
    public string? ConnectionString { get; set; }
    public string? Path { get; set; }
}
