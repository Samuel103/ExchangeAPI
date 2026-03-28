using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ExchangeAPI.Parser.Execution;

namespace ExchangeAPI.Parser;

public class HttpRequestStep : IHandlerStep
{
    public string Method { get; set; } = HttpMethod.Get.Method;
    public string UrlTemplate { get; set; } = string.Empty;
    public string OutputVariable { get; set; } = "httpResponse";
    public XElement? BodyExpression { get; set; }
    public string? BodyTemplate { get; set; }
    public IReadOnlyList<HttpRequestHeader> Headers { get; set; } = [];
    public IReadOnlyList<HttpRequestQueryParam> QueryParams { get; set; } = [];

    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequestStep(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient();
        var url = BuildUrl(context);
        using var request = new HttpRequestMessage(new HttpMethod(Method), url);

        foreach (var header in Headers)
        {
            var headerValue = VariableInterpolator.InterpolateString(header.ValueTemplate, context.Variables);
            request.Headers.TryAddWithoutValidation(header.Name, headerValue);
        }

        if (Method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase)
            || Method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase)
            || Method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase))
        {
            var bodyValue = BodyExpression is not null
                ? ValueExpressionEvaluator.Evaluate(BodyExpression, context)
                : VariableInterpolator.Interpolate(BodyTemplate, context.Variables);

            request.Content = BuildContent(bodyValue);
        }

        using var response = await client.SendAsync(request, cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseBody = TryParseJson(responseText);

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(k => k.Key, v => string.Join(",", v.Value), StringComparer.OrdinalIgnoreCase);

        context.SetVariable(OutputVariable, new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["isSuccess"] = response.IsSuccessStatusCode,
            ["headers"] = headers,
            ["body"] = responseBody,
            ["text"] = responseText
        });
    }

    private string BuildUrl(HandlerExecutionContext context)
    {
        var url = VariableInterpolator.InterpolateString(UrlTemplate, context.Variables);
        if (QueryParams.Count == 0)
        {
            return url;
        }

        var separator = url.Contains('?') ? '&' : '?';
        var query = string.Join("&", QueryParams.Select(param =>
        {
            var value = VariableInterpolator.InterpolateString(param.ValueTemplate, context.Variables);
            return $"{Uri.EscapeDataString(param.Name)}={Uri.EscapeDataString(value)}";
        }));

        return $"{url}{separator}{query}";
    }

    private static HttpContent BuildContent(object? bodyValue)
    {
        if (bodyValue is null)
        {
            return JsonContent.Create(new { });
        }

        if (bodyValue is string bodyText)
        {
            return new StringContent(bodyText, Encoding.UTF8, "application/json");
        }

        return JsonContent.Create(bodyValue)!;
    }

    private static object? TryParseJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return JsonObjectConverter.Convert(document.RootElement);
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}

public record HttpRequestHeader(string Name, string ValueTemplate);
public record HttpRequestQueryParam(string Name, string ValueTemplate);