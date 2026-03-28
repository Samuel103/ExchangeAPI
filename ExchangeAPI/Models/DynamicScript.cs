namespace ExchangeAPI.Models;

public class DynamicScript
{
    public string Name { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public string Time { get; set; } = "00:00";
    public string TimeZone { get; set; } = "Europe/Paris";
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; }
}