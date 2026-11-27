using Microsoft.Data.SqlClient;

namespace Clinic.Application.UnitTests.Common;

public static class SqlServerTestHelper
{
    private static readonly Lazy<(string? ConnectionString, string? SkipReason)> ProbeResult = new(ResolveAndProbe);

    public static string? GetSkipReason() => ProbeResult.Value.SkipReason;

    public static string GetRequiredConnectionString()
    {
        var (cs, reason) = ProbeResult.Value;
        return cs ?? throw new InvalidOperationException(reason ?? "SQL Server integration connection string is unavailable.");
    }

    private static (string? ConnectionString, string? SkipReason) ResolveAndProbe()
    {
        var connStr = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connStr))
        {
            return (null, "Skipped: set TEST_DB_CONNECTION or DB_SA_PASSWORD (env or .env) to run live SQL Server integration tests.");
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connStr)
            {
                ConnectTimeout = 2
            };
            using var conn = new SqlConnection(builder.ConnectionString);
            conn.Open();
            return (connStr, null);
        }
        catch (Exception ex)
        {
            return (null, $"Skipped: SQL Server test database is unreachable ({ex.Message}).");
        }
    }

    private static string? ResolveConnectionString()
    {
        var explicitConn = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(explicitConn))
        {
            return explicitConn.Trim();
        }

        var saPassword = Environment.GetEnvironmentVariable("DB_SA_PASSWORD")
            ?? TryReadEnvVar("DB_SA_PASSWORD");

        if (string.IsNullOrWhiteSpace(saPassword))
        {
            return null;
        }

        var server = Environment.GetEnvironmentVariable("TEST_DB_SERVER") ?? "127.0.0.1,1434";
        return $"Server={server};Database=ClinicDB;User Id=sa;Password={saPassword};TrustServerCertificate=True;MultipleActiveResultSets=true;Connect Timeout=5";
    }

    private static string? TryReadEnvVar(string key)
    {
        foreach (var startDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var envPath = Path.Combine(dir.FullName, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var rawLine in File.ReadAllLines(envPath))
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                        var idx = line.IndexOf('=');
                        if (idx <= 0) continue;
                        if (!string.Equals(line[..idx].Trim(), key, StringComparison.Ordinal)) continue;
                        var val = line[(idx + 1)..].Trim();
                        if ((val.StartsWith('"') && val.EndsWith('"')) || (val.StartsWith('\'') && val.EndsWith('\'')))
                        {
                            val = val[1..^1];
                        }
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
                dir = dir.Parent;
            }
        }
        return null;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var skipReason = SqlServerTestHelper.GetSkipReason();
        if (!string.IsNullOrWhiteSpace(skipReason))
        {
            Skip = skipReason;
        }
    }
}
