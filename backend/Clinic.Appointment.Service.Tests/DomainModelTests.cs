using Clinic.Appointment.Service.Entities;
using Clinic.Appointment.Service.Enums;
using Xunit;

namespace Clinic.Appointment.Service.Tests;

public class DomainModelTests
{
    [Fact]
    public void Appointment_Creation_OnlineBooking_ShouldBePendingAndNotWalkIn()
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var appointment = new Appointment(
            patientId: patientId,
            patientName: "Nguyen Van A",
            patientPhone: "0901234567",
            patientGender: "Male",
            patientDateOfBirth: new DateOnly(1990, 1, 1),
            workScheduleId: scheduleId,
            doctorId: doctorId,
            doctorName: "Dr. House",
            specialtyName: "General",
            scheduleDate: new DateOnly(2026, 11, 1),
            timeSlot: new TimeOnly(9, 0),
            reason: "Headache",
            createdById: creatorId,
            isWalkIn: false);

        Assert.Equal(patientId, appointment.PatientId);
        Assert.Equal("Nguyen Van A", appointment.PatientName);
        Assert.Equal(doctorId, appointment.DoctorId);
        Assert.Equal("Dr. House", appointment.DoctorName);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
        Assert.False(appointment.IsWalkIn);
        Assert.False(appointment.IsDeleted);
        Assert.Empty(appointment.Snapshots);
    }

    [Fact]
    public void Appointment_Creation_WalkIn_ShouldBeConfirmedAndWalkIn()
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var scheduleId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var appointment = new Appointment(
            patientId: patientId,
            patientName: "Tran Thi B",
            patientPhone: "0912345678",
            patientGender: "Female",
            patientDateOfBirth: new DateOnly(1995, 5, 10),
            workScheduleId: scheduleId,
            doctorId: doctorId,
            doctorName: "Dr. Strange",
            specialtyName: "Neurology",
            scheduleDate: new DateOnly(2026, 11, 2),
            timeSlot: new TimeOnly(10, 0),
            reason: "Checkup",
            createdById: creatorId,
            isWalkIn: true);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.True(appointment.IsWalkIn);
    }

    [Fact]
    public void Appointment_Confirm_WhenPending_ShouldTransitionToConfirmed()
    {
        var appointment = CreatePendingAppointment();
        var receptionistId = Guid.NewGuid();

        appointment.Confirm(receptionistId, "Receptionist User");

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.Equal(receptionistId, appointment.UpdatedByUserId);
        Assert.NotNull(appointment.UpdatedAt);
    }

    [Fact]
    public void Appointment_Confirm_WhenCancelled_ShouldThrowInvalidOperationException()
    {
        var appointment = CreatePendingAppointment();
        appointment.Cancel(Guid.NewGuid(), true, "Admin");

        Assert.Throws<InvalidOperationException>(() => appointment.Confirm(Guid.NewGuid()));
    }

    [Fact]
    public void Appointment_CheckIn_WhenConfirmed_ShouldTransitionToCheckedIn()
    {
        var appointment = CreatePendingAppointment();
        appointment.Confirm(Guid.NewGuid());

        var checkInTime = DateTime.UtcNow;
        appointment.CheckIn(checkInTime);

        Assert.Equal(AppointmentStatus.CheckedIn, appointment.Status);
        Assert.Equal(checkInTime, appointment.CheckInTime);
    }

    [Fact]
    public void Appointment_CheckIn_WhenPending_ShouldThrowInvalidOperationException()
    {
        var appointment = CreatePendingAppointment();

        Assert.Throws<InvalidOperationException>(() => appointment.CheckIn(DateTime.UtcNow));
    }

    [Fact]
    public void Appointment_Cancel_WhenActive_ShouldTransitionToCancelled()
    {
        var appointment = CreatePendingAppointment();
        var userId = Guid.NewGuid();

        appointment.Cancel(userId, isAdminOrReceptionist: false, updatorName: "Patient");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(userId, appointment.UpdatedByUserId);
    }

    [Fact]
    public void Appointment_Cancel_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        var appointment = CreatePendingAppointment();
        appointment.Confirm(Guid.NewGuid());
        appointment.CheckIn(DateTime.UtcNow);
        appointment.Status = AppointmentStatus.Completed;

        Assert.Throws<InvalidOperationException>(() => appointment.Cancel(Guid.NewGuid(), true));
    }

    [Fact]
    public void Appointment_Reschedule_ShouldCaptureSnapshotAndApplyNewDetails()
    {
        var oldScheduleId = Guid.NewGuid();
        var oldDoctorId = Guid.NewGuid();
        var oldDate = new DateOnly(2026, 11, 1);
        var oldSlot = new TimeOnly(8, 30);

        var appointment = new Appointment(
            patientId: Guid.NewGuid(),
            patientName: "Nguyen Van A",
            patientPhone: "0901234567",
            patientGender: "Male",
            patientDateOfBirth: new DateOnly(1990, 1, 1),
            workScheduleId: oldScheduleId,
            doctorId: oldDoctorId,
            doctorName: "Dr. Old",
            specialtyName: "Cardiology",
            scheduleDate: oldDate,
            timeSlot: oldSlot,
            reason: "Routine",
            createdById: Guid.NewGuid(),
            isWalkIn: false);

        var newScheduleId = Guid.NewGuid();
        var newDoctorId = Guid.NewGuid();
        var newDate = new DateOnly(2026, 11, 5);
        var newSlot = new TimeOnly(14, 0);
        var updaterId = Guid.NewGuid();

        appointment.Reschedule(
            newWorkScheduleId: newScheduleId,
            newDoctorId: newDoctorId,
            newDoctorName: "Dr. New",
            newSpecialtyName: "Pediatrics",
            newScheduleDate: newDate,
            newTimeSlot: newSlot,
            newReason: "Rescheduled checkup",
            updatedByUserId: updaterId,
            updatorName: "Receptionist Alice");

        // Verify state
        Assert.Equal(newScheduleId, appointment.WorkScheduleId);
        Assert.Equal(newDoctorId, appointment.DoctorId);
        Assert.Equal("Dr. New", appointment.DoctorName);
        Assert.Equal("Pediatrics", appointment.SpecialtyName);
        Assert.Equal(newDate, appointment.ScheduleDate);
        Assert.Equal(newSlot, appointment.TimeSlot);
        Assert.Equal("Rescheduled checkup", appointment.Reason);

        // Verify Snapshot (Audit changelog traceback)
        Assert.Single(appointment.Snapshots);
        var snapshot = appointment.Snapshots.First();
        Assert.Equal(appointment.Id, snapshot.AppointmentId);
        Assert.Equal(oldDoctorId, snapshot.OldDoctorId);
        Assert.Equal("Dr. Old", snapshot.OldDoctorName);
        Assert.Equal("Cardiology", snapshot.OldSpecialtyName);
        Assert.Equal(oldDate, snapshot.OldScheduleDate);
        Assert.Equal(oldSlot, snapshot.TimeSlot);
        Assert.Equal("Routine", snapshot.Reason);
        Assert.Equal(updaterId, snapshot.UpdatedByUserId);
        Assert.Equal("Receptionist Alice", snapshot.UpdatedByUserName);
    }

    private static Appointment CreatePendingAppointment()
    {
        return new Appointment(
            patientId: Guid.NewGuid(),
            patientName: "Le Van C",
            patientPhone: "0987654321",
            patientGender: "Male",
            patientDateOfBirth: new DateOnly(1985, 3, 15),
            workScheduleId: Guid.NewGuid(),
            doctorId: Guid.NewGuid(),
            doctorName: "Dr. Watson",
            specialtyName: "General",
            scheduleDate: new DateOnly(2026, 11, 10),
            timeSlot: new TimeOnly(9, 30),
            reason: "Fever",
            createdById: Guid.NewGuid(),
            isWalkIn: false);
    }
}
