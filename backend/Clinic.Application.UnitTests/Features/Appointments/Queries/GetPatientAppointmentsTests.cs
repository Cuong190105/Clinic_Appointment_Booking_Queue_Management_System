using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Queries;

public sealed class GetPatientAppointmentsTests
{
    [Fact]
    public async Task Gets_own_appointments_using_patient_id_claim()
    {
        var patients = new FakePatientRepository();
        var appointments = new FakeAppointmentRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new GetPatientAppointmentsQueryHandler(appointments, patients, currentUser);
        var patient = TestDataFactory.CreatePatient();
        var schedule = TestDataFactory.CreateWorkSchedule(date: DateOnly.FromDateTime(DateTime.UtcNow));
        var completed = TestDataFactory.CreateAppointment(patient, schedule, checkedIn: true);
        var pending = TestDataFactory.CreateAppointment(patient, schedule, schedule.ShiftStart.AddMinutes(30));
        completed.Complete(patient.Person.IdentityUserId!.Value);
        await appointments.AddAsync(completed);
        await appointments.AddAsync(pending);
        await patients.AddAsync(patient);
        currentUser.UserId = patient.Person.IdentityUserId!.Value;
        currentUser.PatientId = patient.Id;

        var upcoming = await handler.Handle(new(currentUser.UserId.Value), default);
        Assert.Single(upcoming);
        Assert.Equal(AppointmentStatus.Pending, upcoming[0].Status);
        var completedResult = await handler.Handle(new(currentUser.UserId.Value, "Completed"), default);
        Assert.Single(completedResult);
        Assert.Equal(AppointmentStatus.Completed, completedResult[0].Status);
    }

    [Fact]
    public async Task Allows_privileged_staff_to_query_another_patient()
    {
        var patients = new FakePatientRepository();
        var appointments = new FakeAppointmentRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new GetPatientAppointmentsQueryHandler(appointments, patients, currentUser);
        var receptionist = Guid.NewGuid();
        currentUser.UserId = receptionist;
        currentUser.GrantRole("Receptionist");
        var missingPatientId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(receptionist, "Completed", missingPatientId), default));
    }

    [Fact]
    public async Task Rejects_missing_identity_and_patient_cross_access()
    {
        var patients = new FakePatientRepository();
        var appointments = new FakeAppointmentRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new GetPatientAppointmentsQueryHandler(appointments, patients, currentUser);
        var patient = TestDataFactory.CreatePatient();
        currentUser.UserId = patient.Person.IdentityUserId!.Value;
        currentUser.PatientId = patient.Id;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(patient.Person.IdentityUserId!.Value, "upcoming", Guid.NewGuid()), default));
    }
}

