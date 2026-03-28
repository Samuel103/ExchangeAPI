using System.Text.RegularExpressions;
using ExchangeAPI.Parser.Execution;
using Microsoft.Data.SqlClient;

namespace ExchangeAPI.Parser;

public class SqlQueryStep : IHandlerStep
{
    public string Source { get; set; } = string.Empty;
    public string QueryTemplate { get; set; } = string.Empty;
    public string OutputVariable { get; set; } = string.Empty;

    private readonly string _connectionString;

    public SqlQueryStep(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task ExecuteAsync(HandlerExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Convert {{varName}} → @varName to build a safe parameterized query
        var paramNames = new List<string>();
        var parameterizedQuery = Regex.Replace(QueryTemplate, @"\{\{(\w+)\}\}", m =>
        {
            paramNames.Add(m.Groups[1].Value);
            return $"@{m.Groups[1].Value}";
        });

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new SqlCommand(parameterizedQuery, conn);
        cmd.CommandTimeout = 30;

        foreach (var paramName in paramNames)
        {
            var value = context.GetVariable(paramName) ?? DBNull.Value;
            cmd.Parameters.AddWithValue($"@{paramName}", value);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(OutputVariable))
        {
            context.SetVariable(OutputVariable, rows);
        }
    }
}
