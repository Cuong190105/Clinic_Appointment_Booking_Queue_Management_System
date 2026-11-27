using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Clinic.Infrastructure.Sqlserver.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands;

public class WalkInSqlServerIntegrationTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestHelper.GetRequiredConnectionString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [SqlServerFact]
    public async Task SqlServer_CreateWalkInAppointment_PersistsWithoutDetachedWorkScheduleReinsert()
    {

        var tc = new TimeConverter();
        var todayLocal = tc.Today;
        var nowTime = tc.CurrentTime;
        var doctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");
        var patientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
        var userId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
        var scheduleId = Guid.NewGuid();
        var idempotencyKey = "walkin-sql-" + Guid.NewGuid();

        Guid specialtyId;
        await using (var setupContext = CreateDbContext())
        {
            specialtyId = await setupContext.WorkHistories
                .Where(wh => wh.DoctorId == doctorId && wh.EndDate == null && !wh.IsDeleted)
                .Select(wh => wh.SpecialtyId)
                .FirstAsync();

            var schedule = WorkSchedule.CreateForTest(
                scheduleId,
                doctorId,
                todayLocal,
                new TimeOnly(0, 0),
                new TimeOnly(23, 59, 59),
                10,
                WorkScheduleStatus.Active);
            await setupContext.WorkSchedules.AddAsync(schedule);
            await setupContext.SaveChangesAsync();
        }

        try
        {
            await using var context = CreateDbContext();
            var handler = new CreateWalkInAppointmentHandler(
                new PatientRepository(context),
                new WorkScheduleRepository(context),
                new AppointmentRepository(context),
                new QueueTicketRepository(context),
                new UnitOfWork(context));

            var result = await handler.Handle(
                new CreateWalkInAppointmentCommand(userId, patientId, specialtyId, doctorId, "Real SQL walk-in test", true, idempotencyKey),
                CancellationToken.None);

            Assert.NotEqual(Guid.Empty, result.AppointmentId);
            Assert.NotNull(result.QueueTicketId);
            Assert.Equal(AppointmentStatus.CheckedIn.ToString(), result.Status);

            await using var verifyContext = CreateDbContext();
            var savedAppointment = await verifyContext.Appointments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == result.AppointmentId);
            Assert.NotNull(savedAppointment);
            Assert.True(savedAppointment.IsWalkIn);
            Assert.Equal(scheduleId, savedAppointment.WorkScheduleId);

            var savedTicket = await verifyContext.QueueTickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.AppointmentId == result.AppointmentId);
            Assert.NotNull(savedTicket);
            Assert.Equal(todayLocal, savedTicket.QueueDate);
        }
        finally
        {
            await using var cleanupContext = CreateDbContext();
            var appts = await cleanupContext.Appointments.Where(a => a.WorkScheduleId == scheduleId).ToListAsync();
            var apptIds = appts.Select(a => a.Id).ToList();
            var tickets = await cleanupContext.QueueTickets.Where(t => apptIds.Contains(t.AppointmentId)).ToListAsync();
            var snapshots = await cleanupContext.AppointmentSnapshots.Where(s => apptIds.Contains(s.AppointmentId)).ToListAsync();
            cleanupContext.QueueTickets.RemoveRange(tickets);
            cleanupContext.AppointmentSnapshots.RemoveRange(snapshots);
            cleanupContext.Appointments.RemoveRange(appts);
            var sched = await cleanupContext.WorkSchedules.FirstOrDefaultAsync(w => w.Id == scheduleId);
            if (sched != null) cleanupContext.WorkSchedules.Remove(sched);
            await cleanupContext.SaveChangesAsync();
        }
    }

    [SqlServerFact]
    public async Task SqlServer_CreateWalkInAppointment_RejectsTomorrowOrEndedShift()
    {
        var tc = new TimeConverter();
        var todayLocal = tc.Today;
        var tomorrowLocal = todayLocal.AddDays(1);
        var doctorId = Guid.Parse("a0000004-0000-0000-0000-000000000001");
        var patientId = Guid.Parse("a0000005-0000-0000-0000-000000000001");
        var userId = Guid.Parse("a0000002-0000-0000-0000-000000000023");
        var tomorrowScheduleId = Guid.NewGuid();

        Guid specialtyId;
        await using (var setupContext = CreateDbContext())
        {
            specialtyId = await setupContext.WorkHistories
                .Where(wh => wh.DoctorId == doctorId && wh.EndDate == null && !wh.IsDeleted)
                .Select(wh => wh.SpecialtyId)
                .FirstAsync();

            // Seed an isolated Doctor with ONLY a tomorrow schedule to verify ConflictException is thrown
            var futurePerson = new Person("Test Tomorrow Doctor", "0900000099", "tomorrow@clinic.test", new DateOnly(1985, 1, 1), Gender.Male, "Clinic");
            await setupContext.Persons.AddAsync(futurePerson);
            var futureEmployee = new Employee(futurePerson, todayLocal.AddYears(-1));
            await setupContext.Employees.AddAsync(futureEmployee);
            var futureSpecialty = await setupContext.Specialties.FirstAsync(s => s.Id == specialtyId);
            var futureDoctor = new Doctor(futureEmployee, "LIC-" + Guid.NewGuid().ToString("N")[..8], "MD", futureSpecialty);
            await setupContext.Doctors.AddAsync(futureDoctor);
            var tomorrowSchedule = WorkSchedule.CreateForTest(
                tomorrowScheduleId,
                futureDoctor.Id,
                tomorrowLocal,
                new TimeOnly(8, 0),
                new TimeOnly(12, 0),
                10,
                WorkScheduleStatus.Active);
            await setupContext.WorkSchedules.AddAsync(tomorrowSchedule);
            await setupContext.SaveChangesAsync();

            try
            {
                await using var context = CreateDbContext();
                var handler = new CreateWalkInAppointmentHandler(
                    new PatientRepository(context),
                    new WorkScheduleRepository(context),
                    new AppointmentRepository(context),
                    new QueueTicketRepository(context),
                    new UnitOfWork(context));

                await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
                    new CreateWalkInAppointmentCommand(userId, patientId, specialtyId, futureDoctor.Id, "Tomorrow walk-in", false, null),
                    CancellationToken.None));
            }
            finally
            {
                await using var cleanupContext = CreateDbContext();
                var sched = await cleanupContext.WorkSchedules.FirstOrDefaultAsync(w => w.Id == tomorrowScheduleId);
                if (sched != null) cleanupContext.WorkSchedules.Remove(sched);
                var whs = await cleanupContext.WorkHistories.Where(w => w.DoctorId == futureDoctor.Id).ToListAsync();
                cleanupContext.WorkHistories.RemoveRange(whs);
                var doc = await cleanupContext.Doctors.FirstOrDefaultAsync(d => d.Id == futureDoctor.Id);
                if (doc != null) cleanupContext.Doctors.Remove(doc);
                var emp = await cleanupContext.Employees.FirstOrDefaultAsync(e => e.Id == futureEmployee.Id);
                if (emp != null) cleanupContext.Employees.Remove(emp);
                var per = await cleanupContext.Persons.FirstOrDefaultAsync(p => p.Id == futurePerson.Id);
                if (per != null) cleanupContext.Persons.Remove(per);
                await cleanupContext.SaveChangesAsync();
            }
        }
    }
}
