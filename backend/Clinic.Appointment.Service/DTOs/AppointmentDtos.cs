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
    bool IsWalkIn = false,
    Guid WorkScheduleId = default
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
    int CancelledAppointments,
    int NoShowAppointments = 0,
    int AppointmentCount = 0,
    int AppointmentOnlineCount = 0,
    int CheckedIn = 0,
    int CanceledAppointments = 0,
    double CancellationRate = 0,
    double AverageWaitingMinutes = 0
);

public record SyncWalkInAppointmentRequest(
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    string? PatientPhone,
    string? PatientGender,
    DateOnly? PatientDateOfBirth,
    Guid WorkScheduleId,
    Guid DoctorId,
    string DoctorName,
    string? SpecialtyName,
    DateOnly ScheduleDate,
    TimeOnly TimeSlot,
    string Reason,
    Guid CreatedByUserId,
    bool IsUrgent = false
);

public record PagedAppointmentsDto(
    List<AppointmentDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);
