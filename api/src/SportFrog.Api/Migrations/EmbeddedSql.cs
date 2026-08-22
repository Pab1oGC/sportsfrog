using System.Reflection;

namespace SportFrog.Api.Migrations;

/// <summary>
/// Reads a migration's SQL from the assembly's embedded resources. The
/// schema is kept as SQL rather than as strings inside C#: that's the format
/// it gets reviewed in, diffed between versions, and run by hand against the
/// database when needed.
/// </summary>
internal static class EmbeddedSql
{
    private const string ResourcePrefix = "SportFrog.Api.Migrations.Sql.";

    public static string Read(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + fileName;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. " +
                "Check the EmbeddedResource ItemGroup in SportFrog.Api.csproj.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
