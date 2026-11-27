namespace Clinic.Identity.Service.Auth;

/// <summary>
/// Single source of truth for queue permissions and which role holds them. Identity reconciles these
/// grants on every startup (SeedRolesAsync), and the legacy importer
/// (Persistence/import-from-clinicdb.sql, block "queue-permission-map") must apply the same map;
/// QueuePermissionCatalogTests fails if the two diverge.
/// </summary>
public static class QueuePermissionCatalog
{
    public static readonly string[] Permissions =
    [
        "queue.view", "doctor.queue.view", "queue.check-in", "queue.call-next", "queue.skip", "queue.skip-waiting",
        "queue.retry", "queue.priority", "queue.start-exam.own", "queue.start-exam.any",
        "queue.complete-exam.own", "queue.complete-exam.any"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> RoleGrants =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = Permissions,
            ["Receptionist"] = ["queue.view", "queue.check-in", "queue.call-next", "queue.skip-waiting", "queue.retry", "queue.priority"],
            ["Doctor"] = ["doctor.queue.view", "queue.call-next", "queue.skip", "queue.start-exam.own", "queue.complete-exam.own"]
        };
}
