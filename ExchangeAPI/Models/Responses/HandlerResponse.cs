namespace ExchangeAPI.Models.Responses;

public class HandlerResponse
{
    public int StatusCode { get; set; } = StatusCodes.Status200OK;
    public object? Data { get; set; } = new Dictionary<string, object?>
    {
        ["success"] = true
    };
}
