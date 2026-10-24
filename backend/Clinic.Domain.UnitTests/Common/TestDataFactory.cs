using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Domain.UnitTests.Common;

internal static class TestDataFactory
{
    internal static Person CreatePerson(string fullName = "John Doe")
    {
        return new Person(
            fullName,
            "0900000000",
            "john@example.test",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-30)),
            Gender.Male,
            "Test address");
    }

    internal static Patient CreatePatient(Guid? patientId = null)
    {
        var person = CreatePerson();
        return new Patient(
            patientId ?? Guid.NewGuid(),
            person.Id,
            "TEST-INSURANCE",
            "0900000000",
            DateTime.UtcNow,
            null,
            false);
    }

    internal static Appointment CreateAppointment(
        bool confirmed = false,
        bool checkedIn = false,
        int day = 1,
        Guid? patientId = null)
    {
        var patient = CreatePatient(patientId);
        var scheduleDate = new Clinic.Domain.Common.TimeConverter().Today.AddDays(day);
        var appointment = new Appointment(
            Guid.NewGuid(),
            patient.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Doctor Name",
            "Cardiology",
            scheduleDate,
            checkedIn ? new TimeOnly(9, 0) : new TimeOnly(23, 0),
            "Test reason",
            AppointmentStatus.Pending,
            false,
            Guid.NewGuid(),
            DateTime.UtcNow,
            null,
            false);

        typeof(Appointment).GetProperty(nameof(Appointment.Patient))!.SetValue(appointment, patient);
        if (confirmed || checkedIn)
        {
            appointment.Confirm(Guid.NewGuid());
        }

        if (checkedIn)
        {
            appointment.SetDoctorDetails(appointment.DoctorId, appointment.DoctorName, appointment.SpecialtyName, new Clinic.Domain.Common.TimeConverter().Today);
            appointment.CheckIn(DateTime.UtcNow, 1);
        }

        return appointment;
    }

    internal static QueueTicket CreateQueueTicket(Appointment appointment)
    {
        return appointment.QueueTicket ?? appointment.CheckIn(DateTime.UtcNow, 1);
    }
}