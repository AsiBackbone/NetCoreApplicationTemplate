
namespace ProjectTemplate.Infrastructure.Data.Options;

public sealed class DataAccessOptions
{
    public const string SectionName = "ProjectTemplate:DataAccess";

    public const string SqliteProvider = "Sqlite";

    public const string SqlServerProvider = "SqlServer";

    public const string DisabledProvider = "None";

    public const string DisabledProviderAlias = "Disabled";

    public string Provider { get; init; } = SqliteProvider;

    public string ConnectionStringName { get; init; } = "ApplicationDatabase";

    public DataDiagnosticsOptions Diagnostics { get; init; } = new();

    public DataAuditingOptions Auditing { get; init; } = new();

    public static bool IsDisabledProvider(string? provider)
    {
        string normalizedProvider = provider?.Trim() ?? string.Empty;

        return normalizedProvider.Equals(DisabledProvider, StringComparison.OrdinalIgnoreCase)
            || normalizedProvider.Equals(DisabledProviderAlias, StringComparison.OrdinalIgnoreCase);
    }
}
