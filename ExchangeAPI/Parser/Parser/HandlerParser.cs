namespace ExchangeAPI.Parser;

using System.Xml.Linq;
using ExchangeAPI.Contracts;
using ExchangeAPI.Parser.Execution;

public class HandlerParser : IHandlerParser
{
    private readonly ISourceRegistry _sources;

    public HandlerParser(ISourceRegistry sources)
    {
        _sources = sources;
    }

    public IReadOnlyList<IHandlerStep> Parse(string path)
    {
        var doc = XDocument.Load(path);
        var steps = new List<IHandlerStep>();

        if (doc.Root is null)
        {
            return steps;
        }

        foreach (var element in doc.Root.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "Log":
                    steps.Add(new LogStep
                    {
                        Message = element.Attribute("Message")?.Value ?? string.Empty
                    });
                    break;

                case "Set":
                    steps.Add(new SetStep
                    {
                        Variable = element.Attribute("Var")?.Value ?? string.Empty,
                        ValueTemplate = element.Attribute("Value")?.Value
                    });
                    break;

                case "SqlQuery":
                    var sqlSource = element.Attribute("Source")?.Value ?? string.Empty;
                    steps.Add(new SqlQueryStep(_sources.GetConnectionString(sqlSource))
                    {
                        Source = sqlSource,
                        QueryTemplate = element.Attribute("Query")?.Value ?? string.Empty,
                        OutputVariable = element.Attribute("Var")?.Value ?? string.Empty
                    });
                    break;

                case "FileRead":
                    var fileSource = element.Attribute("Source")?.Value ?? string.Empty;
                    steps.Add(new FileReadStep(_sources.GetFilePath(fileSource))
                    {
                        Source = fileSource,
                        OutputVariable = element.Attribute("Var")?.Value ?? string.Empty
                    });
                    break;

                case "Return":
                    steps.Add(new ReturnStep
                    {
                        StatusCode = ParseStatusCode(element),
                        DataTemplate = ParseReturnData(element)
                    });
                    break;
            }
        }

        return steps;
    }

    private static int ParseStatusCode(XElement element)
    {
        var rawStatus = element.Attribute("Status")?.Value;
        return int.TryParse(rawStatus, out var statusCode)
            ? statusCode
            : StatusCodes.Status200OK;
    }

    private static string? ParseReturnData(XElement element)
    {
        var varName = element.Element("Data")?.Attribute("Var")?.Value;
        return string.IsNullOrWhiteSpace(varName) ? null : $"{{{{{varName}}}}}";
    }
}