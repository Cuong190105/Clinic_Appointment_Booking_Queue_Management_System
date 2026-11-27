using System.Text.RegularExpressions;
using Clinic.Identity.Service.Auth;

namespace Clinic.Identity.Service.Tests;

/// <summary>
/// The legacy importer runs after Identity has seeded and reconciled queue permissions, so its
/// queue-permission-map must grant exactly what QueuePermissionCatalog grants. Otherwise permissions
/// change depending on whether Identity restarted after the import.
/// </summary>
public sealed class QueuePermissionCatalogTests
{
    private static string ImportScript() => File.ReadAllText(Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "../../../../Clinic.Identity.Service/Persistence/import-from-clinicdb.sql")));

    [Fact]
    public void LegacyImporter_QueueGrants_MatchCatalog()
    {
        var script = ImportScript();
        var block = Regex.Match(script, "-- BEGIN queue-permission-map(?<body>.*?)-- END queue-permission-map", RegexOptions.Singleline);
        Assert.True(block.Success, "import-from-clinicdb.sql must contain the queue-permission-map block.");

        var sqlGrants = Regex.Matches(block.Groups["body"].Value, @"\(N'(?<role>[^']+)',\s*N'(?<permission>[^']+)'\)")
            .Select(m => $"{m.Groups["role"].Value}:{m.Groups["permission"].Value}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalogGrants = QueuePermissionCatalog.RoleGrants
            .SelectMany(pair => pair.Value.Select(permission => $"{pair.Key}:{permission}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(catalogGrants.SetEquals(sqlGrants),
            $"Only in catalog: [{string.Join(", ", catalogGrants.Except(sqlGrants))}]; only in SQL: [{string.Join(", ", sqlGrants.Except(catalogGrants))}]");
    }

    [Fact]
    public void LegacyImporter_MatchesRolesAndPermissionsByName()
    {
        // Identity creates queue permissions with its own ids before the import runs; an id-only
        // existence check would hit the unique Name index and roll back the whole import.
        var script = ImportScript();
        Assert.Contains("target.Id = p.Id OR target.Name = p.Name", script);
        Assert.Contains("target.Id = r.Id OR target.Name = r.Name", script);
    }

    [Fact]
    public void Catalog_GrantsOnlyKnownPermissions_AndAdminHoldsAll()
    {
        var known = QueuePermissionCatalog.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(QueuePermissionCatalog.RoleGrants.Values.SelectMany(x => x), permission => Assert.Contains(permission, known));
        Assert.True(known.SetEquals(QueuePermissionCatalog.RoleGrants["Admin"]));
        Assert.Contains("queue.retry", QueuePermissionCatalog.RoleGrants["Receptionist"]);
        Assert.Contains("queue.skip-waiting", QueuePermissionCatalog.RoleGrants["Receptionist"]);
    }
}
