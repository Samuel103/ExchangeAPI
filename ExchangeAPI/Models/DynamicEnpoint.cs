namespace ExchangeAPI.Models;

public class DynamicEndpoint
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Handler { get; set; } = string.Empty;
}