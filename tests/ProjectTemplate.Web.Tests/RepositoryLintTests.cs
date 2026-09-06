namespace ProjectTemplate.Web.Tests;

/// <summary>
/// Checks that apply to this repository's own sources rather than to generated application behavior.
/// </summary>
/// <remarks>
/// These read files from the repository working tree, including <c>.template.content</c>, which exists only here and
/// never in a generated project. They are excluded from the template for that reason: a consumer running them would
/// be asserting things about a layout they do not have, or about coding choices that are theirs to make.
/// </remarks>
public sealed class RepositoryLintTests
{
    private static readonly string[] _unsafeRawSqlPatterns =
    [
        "FromSqlRaw",
        "ExecuteSqlRaw",
        "SqlQueryRaw",
        "CommandText",
        "CreateCommand",
        "new SqlCommand"
    ];

    /// <summary>
    /// Verifies that repository sources avoid raw SQL and manual command construction patterns.
    /// </summary>
    /// <remarks>
    /// This is a convention for the template's own code. A generated application may have a legitimate reason to use
    /// ADO.NET directly, which is why this check does not ship.
    /// </remarks>
    [Fact]
    public void SourceFiles_DoNotUseUnsafeRawSqlOrManualCommandConstructionPatterns()
    {
        string solutionRoot = GetSolutionRoot();

        string[] searchRoots =
        [
            Path.Combine(solutionRoot, "src"),
            Path.Combine(solutionRoot, ".template.content", "src")
        ];

        List<string> violations = [];

        foreach (string searchRoot in searchRoots.Where(Directory.Exists))
        {
            foreach (string filePath in Directory.EnumerateFiles(searchRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(filePath);

                foreach (string pattern in _unsafeRawSqlPatterns)
                {
                    if (source.Contains(pattern, StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(solutionRoot, filePath)} contains '{pattern}'.");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Unsafe raw SQL or manual command construction patterns were found. " +
            "Prefer LINQ, FromSqlInterpolated, ExecuteSqlInterpolated, or explicit DbParameter usage for dynamic values." +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Verifies that no repository appsettings file carries a stale OpenTelemetry service version value.
    /// </summary>
    /// <remarks>
    /// Reads both <c>src</c> and the <c>.template.content</c> overlay, so it only makes sense in this repository.
    /// </remarks>
    [Fact]
    public void Appsettings_DoNotContainStaleOpenTelemetryServiceVersion()
    {
        string solutionRoot = GetSolutionRoot();

        List<string> appsettingsPaths = [];

        string srcDirectory = Path.Combine(solutionRoot, "src");

        if (Directory.Exists(srcDirectory))
        {
            appsettingsPaths.AddRange(
                Directory.EnumerateFiles(srcDirectory, "appsettings.json", SearchOption.AllDirectories));
        }

        string templateContentAppsettings = Path.Combine(
            solutionRoot,
            ".template.content",
            "src",
            "ProjectTemplate.Web",
            "appsettings.json");

        if (File.Exists(templateContentAppsettings))
        {
            appsettingsPaths.Add(templateContentAppsettings);
        }

        Assert.NotEmpty(appsettingsPaths);

        foreach (string appsettingsPath in appsettingsPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string appsettings = File.ReadAllText(appsettingsPath);

            Assert.DoesNotContain(
                "\"ServiceVersion\": \"0.3.1\"",
                appsettings,
                StringComparison.Ordinal);
        }
    }

    private static string GetSolutionRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.EnumerateFiles(directory.FullName, "*.slnx").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate solution root from '{AppContext.BaseDirectory}'.");
    }
}
