namespace Clinic.Appointment.Service.DTOs;

using Clinic.Appointment.Service.Enums;

public record CreateAppointmentRequest(
    Guid WorkScheduleId,
    TimeOnly TimeSlot,
    string Reason,
    Guid? PatientId = null,
    bool IsWalkIn = false,
    string? PatientName = null,
    string? PatientPhone = null,
    string? PatientGender = null,
    DateOnly? PatientDateOfBirth = null
);

public record ReceptionistCreateAppointmentRequest(
    Guid WorkScheduleId,
    TimeOnly TimeSlot,
    string Reason,
    Guid PatientId,
    bool IsWalkIn = false
);

public record UpdateAppointmentRequest(
    Guid NewWorkScheduleId,
    TimeOnly TimeSlot,
    string Reason
);

public record AppointmentDto(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    string PatientName,
    string DoctorName,
    TimeOnly TimeSlot,
    string Reason,
    DateOnly Date,
    AppointmentStatus Status,
    DateTime CreatedAt,
    bool IsWalkIn = false
);

public record AppointmentDetailDto(
    Guid Id,
    Guid PatientId,
    Guid DoctorId,
    string PatientName,
    string? PatientPhone,
    string? PatientGender,
    DateOnly? PatientDateOfBirth,
    string DoctorName,
    string? SpecialtyName,
    TimeOnly TimeSlot,
    DateOnly Date,
    string Reason,
    AppointmentStatus Status,
    DateTime CreatedAt,
    bool IsWalkIn = false
);

public record AppointmentHistoryDto(
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    TimeOnly TimeSlot,
    DateOnly Date,
    string Reason,
    AppointmentStatus Status,
    DateTime UpdatedAt,
    Guid UpdatedByUserId,
    string? UpdatorName
);

public record AppointmentChangelogDto(
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    List<AppointmentHistoryDto> Changelog
);

public record TotalAppointmentSummaryDto(
    int TotalAppointments,
    int PendingAppointments,
    int ConfirmedAppointments,
    int CheckedInAppointments,
    int CompletedAppointments,
    int CancelledAppointments
);
