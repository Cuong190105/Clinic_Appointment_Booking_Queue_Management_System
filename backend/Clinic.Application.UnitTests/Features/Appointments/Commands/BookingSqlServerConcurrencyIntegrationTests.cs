using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands
{
    public class BookingSqlServerConcurrencyIntegrationTests
    {
        private static readonly string ConnectionString =
            Environment.GetEnvironmentVariable("TEST_DB_CONNECTION") ??
            "Server=127.0.0.1,1434;Database=ClinicDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;MultipleActiveResultSets=true;Connect Timeout=5";

        private async Task<bool> IsSqlServerAvailableAsync()
        {
            try
            {
                await using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task SqlServer_ConcurrentBookingSameSlot_ShouldAllowOnlyOneWinnerAndSerializeWithUpdlock()
        {
            Assert.True(await IsSqlServerAvailableAsync(), $"SQL Server container must be running at {ConnectionString} for live concurrency integration testing.");

            var scheduleId = Guid.NewGuid();
            var doctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");
            var patientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
            var userId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
            var slot = new TimeOnly(8, 30);
            var testDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

            // Setup real schedule in SQL Server
            await using (var setupContext = CreateDbContext())
            {
                var schedule = WorkSchedule.CreateForTest(
                    scheduleId,
                    doctorId,
                    testDate,
                    new TimeOnly(8, 0),
                    new TimeOnly(12, 0),
                    10,
                    WorkScheduleStatus.Active
                );
                await setupContext.WorkSchedules.AddAsync(schedule);
                await setupContext.SaveChangesAsync();
            }

            try
            {
                int successCount = 0;
                int conflictCount = 0;
                var barrier = new ManualResetEventSlim(false);

                // Act: 10 concurrent threads against REAL SQL Server
                var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
                {
                    await using var context = CreateDbContext();
                    var wsRepo = new WorkScheduleRepository(context);
                    var pRepo = new PatientRepository(context);
                    var apptRepo = new AppointmentRepository(context);
                    var uow = new UnitOfWork(context);
                    var notiQueue = new FakeNotificationQueue();

                    var handler = new CreateAppointmentCommandHandler(wsRepo, pRepo, apptRepo, uow, notiQueue);
                    var cmd = new CreateAppointmentCommand(
                        userId,
                        scheduleId,
                        slot,
                        $"Real SQL Contention {i}",
                        false,
                        patientId,
                        Guid.NewGuid().ToString()
                    );

                    barrier.Wait();
                    try
                    {
                        var res = await handler.Handle(cmd, CancellationToken.None);
                        if (res != null)
                            Interlocked.Increment(ref successCount);
                    }
                    catch (ConflictException)
                    {
                        Interlocked.Increment(ref conflictCount);
                    }
                }));

                barrier.Set();
                await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(1, successCount);
                Assert.Equal(9, conflictCount);

                // Verify inside database
                await using (var verifyContext = CreateDbContext())
                {
                    var count = await verifyContext.Appointments
                        .CountAsync(a => a.WorkScheduleId == scheduleId && a.TimeSlot == slot);
                    Assert.Equal(1, count);
                }
            }
            finally
            {
                // Cleanup test data from SQL Server
                await using var cleanupContext = CreateDbContext();
                var appts = await cleanupContext.Appointments.Where(a => a.WorkScheduleId == scheduleId).ToListAsync();
                var apptIds = appts.Select(a => a.Id).ToList();
                var snapshots = await cleanupContext.AppointmentSnapshots.Where(s => apptIds.Contains(s.AppointmentId)).ToListAsync();
                cleanupContext.AppointmentSnapshots.RemoveRange(snapshots);
                cleanupContext.Appointments.RemoveRange(appts);
                var sched = await cleanupContext.WorkSchedules.FirstOrDefaultAsync(w => w.Id == scheduleId);
                if (sched != null) cleanupContext.WorkSchedules.Remove(sched);
                await cleanupContext.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task SqlServer_ConcurrentSameIdempotencyKey_ShouldAllSucceedAndReturnSameAppointment()
        {
            Assert.True(await IsSqlServerAvailableAsync(), $"SQL Server container must be running at {ConnectionString} for live concurrency integration testing.");

            var scheduleId = Guid.NewGuid();
            var doctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");
            var patientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
            var userId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
            var slot = new TimeOnly(9, 0);
            var testDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11));
            var sharedIdempotencyKey = "sql-idemp-" + Guid.NewGuid();

            await using (var setupContext = CreateDbContext())
            {
                var schedule = WorkSchedule.CreateForTest(
                    scheduleId,
                    doctorId,
                    testDate,
                    new TimeOnly(8, 0),
                    new TimeOnly(12, 0),
                    10,
                    WorkScheduleStatus.Active
                );
                await setupContext.WorkSchedules.AddAsync(schedule);
                await setupContext.SaveChangesAsync();
            }

            try
            {
                var results = new List<Clinic.Application.Contracts.AppointmentDto>();
                var barrier = new ManualResetEventSlim(false);

                // Act: 8 concurrent threads sending identical Idempotency-Key to SQL Server
                var tasks = Enumerable.Range(0, 8).Select(i => Task.Run(async () =>
                {
                    await using var context = CreateDbContext();
                    var wsRepo = new WorkScheduleRepository(context);
                    var pRepo = new PatientRepository(context);
                    var apptRepo = new AppointmentRepository(context);
                    var uow = new UnitOfWork(context);
                    var notiQueue = new FakeNotificationQueue();

                    var handler = new CreateAppointmentCommandHandler(wsRepo, pRepo, apptRepo, uow, notiQueue);
                    var cmd = new CreateAppointmentCommand(
                        userId,
                        scheduleId,
                        slot,
                        "Real SQL Idempotency Test",
                        false,
                        patientId,
                        sharedIdempotencyKey
                    );

                    barrier.Wait();
                    return await handler.Handle(cmd, CancellationToken.None);
                }));

                barrier.Set();
                var taskResults = await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(8, taskResults.Length);
                var expectedId = taskResults[0].Id;
                Assert.All(taskResults, r => Assert.Equal(expectedId, r.Id));

                // Verify inside database: exactly 1 record created
                await using (var verifyContext = CreateDbContext())
                {
                    var count = await verifyContext.Appointments
                        .CountAsync(a => a.WorkScheduleId == scheduleId && a.IdempotencyKey == sharedIdempotencyKey);
                    Assert.Equal(1, count);
                }
            }
            finally
            {
                await using var cleanupContext = CreateDbContext();
                var appts = await cleanupContext.Appointments.Where(a => a.WorkScheduleId == scheduleId).ToListAsync();
                var apptIds = appts.Select(a => a.Id).ToList();
                var snapshots = await cleanupContext.AppointmentSnapshots.Where(s => apptIds.Contains(s.AppointmentId)).ToListAsync();
                cleanupContext.AppointmentSnapshots.RemoveRange(snapshots);
                cleanupContext.Appointments.RemoveRange(appts);
                var sched = await cleanupContext.WorkSchedules.FirstOrDefaultAsync(w => w.Id == scheduleId);
                if (sched != null) cleanupContext.WorkSchedules.Remove(sched);
                await cleanupContext.SaveChangesAsync();
            }
        }
    }
}
