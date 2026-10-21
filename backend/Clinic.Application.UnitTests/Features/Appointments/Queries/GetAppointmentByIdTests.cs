using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.UnitTests.Common;

namespace Clinic.Application.UnitTests.Features.Appointments.Queries;

public sealed class GetAppointmentByIdTests
{
    [Fact]
    public async Task Allows_receptionist_patient_owner_and_assigned_doctor_from_token_claims()
    {
        var repository = new FakeAppointmentRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new GetAppointmentByIdQueryHandler(repository, currentUser);
        var patient = TestDataFactory.CreatePatient();
        var doctor = TestDataFactory.CreateDoctor();
        var appointment = TestDataFactory.CreateAppointment(patient, TestDataFactory.CreateWorkSchedule(doctor));
        await repository.AddAsync(appointment);

        var receptionistId = Guid.NewGuid();
        currentUser.UserId = receptionistId;
        currentUser.GrantRole("Receptionist");
        Assert.Equal(appointment.Id, (await handler.Handle(new(appointment.Id, receptionistId), default)).Id);

        currentUser.UserId = doctor.Employee.Person.IdentityUserId!.Value;
        currentUser.DoctorId = doctor.Id;
        currentUser.GrantRole("Doctor");
        Assert.Equal(appointment.Id, (await handler.Handle(new(appointment.Id, currentUser.UserId.Value), default)).Id);

        currentUser.UserId = patient.Person.IdentityUserId!.Value;
        currentUser.PatientId = patient.Id;
        currentUser.GrantRole("Patient");
        Assert.Equal(appointment.Id, (await handler.Handle(new(appointment.Id, currentUser.UserId.Value), default)).Id);
    }

    [Fact]
    public async Task Denies_unrelated_users_and_user_id_mismatch()
    {
        var repository = new FakeAppointmentRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new GetAppointmentByIdQueryHandler(repository, currentUser);
        var patient = TestDataFactory.CreatePatient();
        var doctor = TestDataFactory.CreateDoctor();
        var unrelatedDoctor = TestDataFactory.CreateDoctor();
        var appointment = TestDataFactory.CreateAppointment(patient, TestDataFactory.CreateWorkSchedule(doctor));
        await repository.AddAsync(appointment);

        currentUser.UserId = unrelatedDoctor.Employee.Person.IdentityUserId!.Value;
        currentUser.DoctorId = unrelatedDoctor.Id;
        currentUser.GrantRole("Doctor");
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(appointment.Id, currentUser.UserId.Value), default));

        currentUser.UserId = patient.Person.IdentityUserId!.Value;
        currentUser.PatientId = TestDataFactory.CreatePatient().Id;
        currentUser.GrantRole("Patient");
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(appointment.Id, currentUser.UserId.Value), default));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(appointment.Id, Guid.NewGuid()), default));
    }
}

