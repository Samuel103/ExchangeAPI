namespace ExchangeAPI.Parser;

using System.Xml.Linq;
using ExchangeAPI.Contracts;
using ExchangeAPI.Parser.Execution;

public class HandlerParser : IHandlerParser
{
    private readonly ISourceRegistry _sources;
    private readonly IHttpClientFactory _httpClientFactory;

    public HandlerParser(ISourceRegistry sources, IHttpClientFactory httpClientFactory)
    {
        _sources = sources;
        _httpClientFactory = httpClientFactory;
    }

    public IReadOnlyList<IHandlerStep> Parse(string path)
    {
        var doc = XDocument.Load(path);
        if (doc.Root is null)
        {
            return [];
        }

        return ParseSteps(doc.Root.Elements());
    }

    private IReadOnlyList<IHandlerStep> ParseSteps(IEnumerable<XElement> elements)
    {
        var steps = new List<IHandlerStep>();

        foreach (var element in elements)
        {
            switch (element.Name.LocalName.ToLowerInvariant())
            {
                case "log":
                    steps.Add(new LogStep
                    {
                        Message = GetAttribute(element, "Message") ?? string.Empty
                    });
                    break;

                case "set":
                    var setValueElement = GetChild(element, "Value");
                    var setExpression = setValueElement?.Elements().FirstOrDefault();

                    steps.Add(new SetStep
                    {
                        Name = GetAttribute(element, "Name", "Var")
                            ?? string.Empty,
                        ValueTemplate = GetAttribute(element, "Value")
                            ?? GetChildValue(element, "Value"),
                        ValueExpression = setExpression is null ? null : new XElement(setExpression)
                    });
                    break;

                case "int":
                case "string":
                case "datetime":
                case "bool":
                case "decimal":
                case "double":
                case "long":
                    steps.Add(new DeclareVariableStep
                    {
                        TypeName = element.Name.LocalName,
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        InitialValueTemplate = GetAttribute(element, "Value") ?? GetChildValue(element, "Value")
                    });
                    break;

                case "stringtoint":
                    steps.Add(new StringToIntStep
                    {
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        From = GetAttribute(element, "From") ?? string.Empty
                    });
                    break;

                case "inttostring":
                    steps.Add(new IntToStringStep
                    {
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        From = GetAttribute(element, "From") ?? string.Empty
                    });
                    break;

                case "stringformat":
                    steps.Add(new StringFormatStep
                    {
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        Format = GetAttribute(element, "Format") ?? GetChildValue(element, "Format") ?? string.Empty,
                        Vars = GetAttribute(element, "Vars") ?? string.Empty
                    });
                    break;

                case "stringsubstring":
                    steps.Add(new StringSubstringStep
                    {
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        From = GetAttribute(element, "From") ?? string.Empty,
                        Start = ParseIntOrDefault(GetAttribute(element, "Start"), 0),
                        Length = ParseNullableInt(GetAttribute(element, "Length"))
                    });
                    break;

                case "stringtodatetime":
                    steps.Add(new StringToDateTimeStep
                    {
                        Name = GetAttribute(element, "Name") ?? string.Empty,
                        From = GetAttribute(element, "From") ?? string.Empty
                    });
                    break;

                case "sqlquery":
                    var sqlSource = GetAttribute(element, "Source") ?? string.Empty;
                    steps.Add(new SqlQueryStep(_sources.GetConnectionString(sqlSource))
                    {
                        Source = sqlSource,
                        QueryTemplate = ParseSqlTemplate(element),
                        OutputVariable = GetAttribute(element, "Var") ?? string.Empty
                    });
                    break;

                case "if":
                    steps.Add(ParseIfStep(element));
                    break;

                case "try":
                    steps.Add(ParseTryCatchStep(element));
                    break;

                case "foreach":
                    steps.Add(ParseForEachStep(element));
                    break;

                case "http-get":
                case "httpget":
                    steps.Add(ParseHttpRequestStep(element, HttpMethod.Get.Method));
                    break;

                case "http-post":
                case "httppost":
                    steps.Add(ParseHttpRequestStep(element, HttpMethod.Post.Method));
                    break;

                case "fileread":
                    var fileSource = GetAttribute(element, "Source") ?? string.Empty;
                    steps.Add(new FileReadStep(_sources.GetFilePath(fileSource))
                    {
                        Source = fileSource,
                        OutputVariable = GetAttribute(element, "Var") ?? string.Empty,
                        Delimiter = GetAttribute(element, "Delimiter")
                    });
                    break;

                case "return":
                        steps.Add(new ReturnStep
                        {
                            StatusCode = ParseStatusCode(element),
                            DataTemplate = ParseReturnData(element)
                        });
                        break;

                    case "bodyread":
                    steps.Add(new BodyReadStep
                    {
                        OutputVariable = GetAttribute(element, "Var") ?? "body"
                    });
                    break;

                case "sqlexecute":
                    var sqlExecSource = GetAttribute(element, "Source") ?? string.Empty;
                    steps.Add(new SqlExecuteStep(_sources.GetConnectionString(sqlExecSource))
                    {
                        Source = sqlExecSource,
                        QueryTemplate = ParseSqlTemplate(element),
                        OutputVariable = GetAttribute(element, "Var")
                    });
                    break;

                case "csvappend":
                    var csvAppendSource = GetAttribute(element, "Source") ?? string.Empty;
                    steps.Add(new CsvAppendStep(_sources.GetFilePath(csvAppendSource))
                    {
                        Source = csvAppendSource,
                        RowVariable = GetAttribute(element, "Row") ?? string.Empty,
                        Delimiter = GetAttribute(element, "Delimiter") ?? ","
                    });
                    break;

            }
        }

        return steps;
    }

    private IHandlerStep ParseIfStep(XElement element)
    {
        var conditionElement = GetChild(element, "Condition");
        var conditionExpression = conditionElement?.Elements().FirstOrDefault();

        var thenElement = GetChild(element, "Then");
        var elseElement = GetChild(element, "Else");

        var thenSteps = ParseSteps(
            thenElement?.Elements()
            ?? element.Elements().Where(e => !IsNamed(e, "Condition", "Then", "Else")));

        var elseSteps = ParseSteps(elseElement?.Elements() ?? []);

        return new IfElseStep
        {
            ConditionExpression = conditionExpression is null ? null : new XElement(conditionExpression),
            ConditionTemplate = conditionExpression is null
                ? (TrimOrNull(conditionElement?.Value) ?? GetAttribute(element, "Condition"))
                : null,
            ThenSteps = thenSteps,
            ElseSteps = elseSteps
        };
    }

    private IHandlerStep ParseTryCatchStep(XElement element)
    {
        var catchElement = GetChild(element, "Catch");
        var trySteps = ParseSteps(element.Elements().Where(e => !IsNamed(e, "Catch")));
        var catchSteps = ParseSteps(catchElement?.Elements() ?? []);

        return new TryCatchStep
        {
            TrySteps = trySteps,
            CatchSteps = catchSteps,
            ErrorVariable = GetAttribute(catchElement, "Var", "ErrorVar")
                ?? GetAttribute(element, "ErrorVar")
                ?? "error"
        };
    }

    private IHandlerStep ParseForEachStep(XElement element)
    {
        var inElement = GetChild(element, "In", "Collection");
        var collectionExpression = inElement?.Elements().FirstOrDefault();
        var bodyElement = GetChild(element, "Body", "Do");

        var bodyElements = bodyElement?.Elements()
            ?? element.Elements().Where(e => !IsNamed(e, "In", "Collection", "ItemVar", "IndexVar", "Body", "Do"));

        return new ForEachStep
        {
            ItemVariable = GetChildValue(element, "ItemVar")
                ?? GetAttribute(element, "ItemVar", "Item", "Var")
                ?? "item",
            IndexVariable = GetChildValue(element, "IndexVar")
                ?? GetAttribute(element, "IndexVar", "Index"),
            CollectionExpression = collectionExpression is null ? null : new XElement(collectionExpression),
            CollectionTemplate = collectionExpression is null
                ? (TrimOrNull(inElement?.Value) ?? GetAttribute(element, "In", "Collection"))
                : null,
            BodySteps = ParseSteps(bodyElements)
        };
    }

    private IHandlerStep ParseHttpRequestStep(XElement element, string method)
    {
        var url = GetChildValue(element, "Url") ?? GetAttribute(element, "Url") ?? string.Empty;
        var responseElement = GetChild(element, "Response");
        var outputVariable = GetAttribute(responseElement, "Var", "Name")
            ?? GetAttribute(element, "Var", "Output", "OutputVar")
            ?? "httpResponse";

        var headers = GetChild(element, "Headers")?.Elements()
            .Where(e => IsNamed(e, "Header"))
            .Select(e => new HttpRequestHeader(
                GetAttribute(e, "Name", "Key") ?? string.Empty,
                GetAttribute(e, "Value") ?? TrimOrNull(e.Value) ?? string.Empty))
            .Where(h => !string.IsNullOrWhiteSpace(h.Name))
            .ToList()
            ?? [];

        var queryParams = GetChild(element, "Query")?.Elements()
            .Where(e => IsNamed(e, "Param"))
            .Select(e => new HttpRequestQueryParam(
                GetAttribute(e, "Name", "Key") ?? string.Empty,
                GetAttribute(e, "Value") ?? TrimOrNull(e.Value) ?? string.Empty))
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .ToList()
            ?? [];

        var bodyElement = GetChild(element, "Body");
        var bodyExpression = bodyElement?.Elements().FirstOrDefault();

        return new HttpRequestStep(_httpClientFactory)
        {
            Method = method,
            UrlTemplate = url,
            OutputVariable = outputVariable,
            Headers = headers,
            QueryParams = queryParams,
            BodyExpression = bodyExpression is null ? null : new XElement(bodyExpression),
            BodyTemplate = bodyExpression is null ? TrimOrNull(bodyElement?.Value) : null
        };
    }

    private static int ParseStatusCode(XElement element)
    {
        var rawStatus = GetAttribute(element, "Status");
        return int.TryParse(rawStatus, out var statusCode)
            ? statusCode
            : StatusCodes.Status200OK;
    }

    private static string? ParseReturnData(XElement element)
    {
        var dataElement = GetChild(element, "Data");
        var varName = dataElement?.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, "Var", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(varName) ? null : $"{{{{{varName}}}}}";
    }

    private static string ParseSqlTemplate(XElement element)
    {
        var fromAttribute = GetAttribute(element, "Query");
        if (!string.IsNullOrWhiteSpace(fromAttribute))
        {
            return fromAttribute;
        }

        var queryElement = GetChild(element, "Query");
        if (queryElement is not null)
        {
            return SqlXmlQueryBuilder.Build(queryElement);
        }

        throw new InvalidOperationException(
            $"{element.Name.LocalName} requires either Query attribute or Query child element.");
    }

    private static string? GetAttribute(XElement? element, params string[] names)
    {
        if (element is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var attribute = element.Attributes().FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            if (attribute is not null)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    private static XElement? GetChild(XElement element, params string[] names)
    {
        return element.Elements().FirstOrDefault(e => IsNamed(e, names));
    }

    private static string? GetChildValue(XElement element, string childName)
    {
        var value = GetChild(element, childName)?.Value;
        return TrimOrNull(value);
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool IsNamed(XElement element, params string[] names)
    {
        return names.Any(name => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseIntOrDefault(string? raw, int defaultValue)
    {
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static int? ParseNullableInt(string? raw)
    {
        return int.TryParse(raw, out var value) ? value : null;
    }
}