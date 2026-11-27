using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Commands;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Common;
using Clinic.Domain.Common.Exceptions;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Commands;

public class CreateWalkInAppointmentTests
{
    [Fact]
    public async Task Handle_SpecificDoctor_ReloadsTrackedScheduleAndCreatesWalkInTicket()
    {
        var workScheduleRepository = new FakeWorkScheduleRepository();
        var patientRepository = new FakePatientRepository();
        var appointmentRepository = new FakeAppointmentRepository();
        var queueTicketRepository = new FakeQueueTicketRepository();
        var unitOfWork = new FakeUnitOfWork();

        var specialty = TestDataFactory.CreateSpecialty("Cardiology");
        var doctor = TestDataFactory.CreateDoctor(specialty: specialty);
        var patient = TestDataFactory.CreatePatient();
        await patientRepository.AddAsync(patient);

        var todayLocal = new TimeConverter().Today;
        var schedule = TestDataFactory.CreateWorkSchedule(
            doctor: doctor,
            date: todayLocal,
            startTime: new TimeOnly(0, 0),
            endTime: new TimeOnly(23, 59, 59));
        workScheduleRepository.Add(schedule);

        var handler = new CreateWalkInAppointmentHandler(
            patientRepository,
            workScheduleRepository,
            appointmentRepository,
            queueTicketRepository,
            unitOfWork);

        var command = new CreateWalkInAppointmentCommand(
            Guid.NewGuid(),
            patient.Id,
            specialty.Id,
            doctor.Id,
            "Acute fever",
            IsUrgent: true,
            IdempotencyKey: "walkin-key-1");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.AppointmentId);
        Assert.Equal(doctor.Id, result.DoctorId);
        Assert.Equal("Cardiology", result.SpecialtyName);
        Assert.True(result.IsUrgent);
        Assert.Equal(AppointmentStatus.CheckedIn.ToString(), result.Status);
        // Must attach the appointment to the tracked WorkSchedule instance returned by GetByIdAsync
        Assert.Single(schedule.Appointments);
        Assert.Single(queueTicketRepository.All);
    }

    [Fact]
    public async Task Handle_AutoSelectDoctorBySpecialty_SucceedsWhenDoctorIdIsNull()
    {
        var workScheduleRepository = new FakeWorkScheduleRepository();
        var patientRepository = new FakePatientRepository();
        var appointmentRepository = new FakeAppointmentRepository();
        var queueTicketRepository = new FakeQueueTicketRepository();
        var unitOfWork = new FakeUnitOfWork();

        var specialty = TestDataFactory.CreateSpecialty("Pediatrics");
        var doctor = TestDataFactory.CreateDoctor(specialty: specialty);
        var patient = TestDataFactory.CreatePatient();
        await patientRepository.AddAsync(patient);

        var todayLocal = new TimeConverter().Today;
        var schedule = TestDataFactory.CreateWorkSchedule(
            doctor: doctor,
            date: todayLocal,
            startTime: new TimeOnly(0, 0),
            endTime: new TimeOnly(23, 59, 59));
        workScheduleRepository.Add(schedule);

        var handler = new CreateWalkInAppointmentHandler(
            patientRepository,
            workScheduleRepository,
            appointmentRepository,
            queueTicketRepository,
            unitOfWork);

        var command = new CreateWalkInAppointmentCommand(
            Guid.NewGuid(),
            patient.Id,
            specialty.Id,
            DoctorId: null,
            "Walk-in auto assign",
            IsUrgent: false,
            IdempotencyKey: null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(doctor.Id, result.DoctorId);
        Assert.Equal("Pediatrics", result.SpecialtyName);
        Assert.Single(schedule.Appointments);
        Assert.Single(queueTicketRepository.All);
    }

    [Fact]
    public async Task Handle_RejectsEndedShiftToday_YesterdayShift_AndTomorrowShift()
    {
        var workScheduleRepository = new FakeWorkScheduleRepository();
        var patientRepository = new FakePatientRepository();
        var appointmentRepository = new FakeAppointmentRepository();
        var queueTicketRepository = new FakeQueueTicketRepository();
        var unitOfWork = new FakeUnitOfWork();

        var specialty = TestDataFactory.CreateSpecialty("Dermatology");
        var doctor = TestDataFactory.CreateDoctor(specialty: specialty);
        var patient = TestDataFactory.CreatePatient();
        await patientRepository.AddAsync(patient);

        var tc = new TimeConverter();
        var today = tc.Today;
        var now = tc.CurrentTime;

        // Ended shift today (if after 00:02)
        if (now > new TimeOnly(0, 2))
        {
            var endedShift = TestDataFactory.CreateWorkSchedule(
                doctor: doctor,
                date: today,
                startTime: new TimeOnly(0, 0),
                endTime: now.AddMinutes(-1));
            workScheduleRepository.Add(endedShift);
        }

        // Yesterday shift and tomorrow shift must not be selected for today's walk-in
        workScheduleRepository.Add(TestDataFactory.CreateWorkSchedule(
            doctor: doctor, date: today.AddDays(-1), startTime: new TimeOnly(0, 0), endTime: new TimeOnly(23, 59, 59)));
        workScheduleRepository.Add(TestDataFactory.CreateWorkSchedule(
            doctor: doctor, date: today.AddDays(1), startTime: new TimeOnly(0, 0), endTime: new TimeOnly(23, 59, 59)));

        var handler = new CreateWalkInAppointmentHandler(
            patientRepository,
            workScheduleRepository,
            appointmentRepository,
            queueTicketRepository,
            unitOfWork);

        var cmd = new CreateWalkInAppointmentCommand(
            Guid.NewGuid(),
            patient.Id,
            specialty.Id,
            doctor.Id,
            "Invalid shift check",
            IsUrgent: false,
            IdempotencyKey: null);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Empty(queueTicketRepository.All);
    }
}
