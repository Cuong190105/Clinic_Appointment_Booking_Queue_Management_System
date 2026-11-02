using Clinic.Notification.Service.Entities;
using Clinic.Notification.Service.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Notification.Service.Tests;

public sealed class NotificationImportVerificationTests
{
    [Fact]
    public void ImportScript_Exists_And_Enforces_Idempotency_And_Safety()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "../../../../../backend/Clinic.Notification.Service/Persistence/import-from-clinicdb.sql");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath("Persistence/import-from-clinicdb.sql");
        }

        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");
        var content = File.ReadAllText(scriptPath);

        Assert.Contains("INSERT INTO ClinicNotificationDB.dbo.Notifications", content);
        Assert.Contains("FROM ClinicDB.dbo.Notifications", content);
        Assert.Contains("WHERE NOT EXISTS", content);
        Assert.Contains("BEGIN TRANSACTION", content);
        Assert.Contains("COMMIT TRANSACTION", content);
    }

    [Fact]
    public void VerifyScript_Exists_And_Checks_Zero_Mismatches()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "../../../../../backend/Clinic.Notification.Service/Persistence/verify-notifications-import.sql");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath("Persistence/verify-notifications-import.sql");
        }

        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");
        var content = File.ReadAllText(scriptPath);

        Assert.Contains("SourceCount_ClinicDB", content);
        Assert.Contains("TargetCount_ClinicNotificationDB", content);
        Assert.Contains("MissingRecipientCount", content);
        Assert.Contains("DuplicateIdCount", content);
        Assert.Contains("VERIFICATION PASSED", content);
    }

    [Fact]
    public async Task Import_Rerun_Simulation_Is_Idempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var existingId = Guid.NewGuid();
        var initialItem = new NotificationItem
        {
            Id = existingId,
            RecipientId = Guid.NewGuid(),
            RecipientType = RecipientType.Account,
            NotificationType = NotificationType.AppointmentConfirmed,
            Title = "Thông báo ban đầu",
            Body = "Nội dung ban đầu"
        };

        await using (var db = new NotificationDbContext(options))
        {
            db.Notifications.Add(initialItem);
            await db.SaveChangesAsync();
        }

        // Simulate rerun: only insert WHERE NOT EXISTS
        await using (var db = new NotificationDbContext(options))
        {
            var sourceRows = new[]
            {
                new { Id = existingId, Title = "Đã tồn tại" },
                new { Id = Guid.NewGuid(), Title = "Bản ghi mới" }
            };

            foreach (var row in sourceRows)
            {
                var exists = await db.Notifications.AnyAsync(n => n.Id == row.Id);
                if (!exists)
                {
                    db.Notifications.Add(new NotificationItem
                    {
                        Id = row.Id,
                        RecipientId = Guid.NewGuid(),
                        RecipientType = RecipientType.Account,
                        NotificationType = NotificationType.AppointmentConfirmed,
                        Title = row.Title,
                        Body = "Nội dung mới"
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        await using (var db = new NotificationDbContext(options))
        {
            var total = await db.Notifications.CountAsync();
            Assert.Equal(2, total);
        }
    }
}

