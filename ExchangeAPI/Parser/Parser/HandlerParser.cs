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
                        Name = GetAttribute(element, "Name")
                            ?? GetAttribute(element, "Var")
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

    private static string? GetAttribute(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(a =>
            string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static XElement? GetChild(XElement element, string name)
    {
        return element.Elements().FirstOrDefault(e =>
            string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetChildValue(XElement element, string childName)
    {
        var value = GetChild(element, childName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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