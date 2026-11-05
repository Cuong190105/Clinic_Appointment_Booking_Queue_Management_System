using System.Text.Json.Serialization;

namespace Clinic.API.Models;

public class PaginationResponse<T>
{
    [JsonPropertyName("items")]
    public IReadOnlyList<T> Items { get; set; } = new List<T>();

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    [JsonPropertyName("hasPrevious")]
    public bool HasPrevious => PageNumber > 1;

    [JsonPropertyName("hasNext")]
    public bool HasNext => PageNumber < TotalPages;

    public PaginationResponse() { }

    public PaginationResponse(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}

public class UserBriefDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class UserSummaryDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserDetailDto : UserSummaryDto
{
    public Guid PersonId { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
}

public class PatientDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? InsuranceNumber { get; set; } = string.Empty;
    public string? EmergencyContact { get; set; } = string.Empty;
}

public class AppointmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public TimeOnly TimeSlot { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsWalkIn { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppointmentExtended : AppointmentDto
{
    public string SpecialtyName { get; set; } = string.Empty;
    public Guid SpecialtyId { get; set; }
}

public class AppointmentDetailDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public TimeOnly TimeSlot { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public string? QueueNumber { get; set; } = string.Empty;
    public TimeOnly? QueueTime { get; set; }
    public MedicalReportDto? MedicalReport { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppointmentHistoryDto
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public TimeOnly TimeSlot { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string UpdatorName { get; set; } = string.Empty;
}

public class AppointmentChangelogDto
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public IEnumerable<AppointmentHistoryDto> Changelog { get; set; } = Enumerable.Empty<AppointmentHistoryDto>();
}

public class TotalAppointmentSummaryDto
{
    public int AppointmentCount { get; set; }
    public int AppointmentOnlineCount { get; set; }
    public int CompletedAppointments { get; set; }
    public int CanceledAppointments { get; set; }
    public int NoShowAppointments { get; set; }
    public double AverageWaitingMinutes { get; set; }
    public double CancellationRate { get; set; }
}

public class StatisticsDataDto
{
    public string Period { get; set; } = string.Empty;
    public List<AppointmentByDayDto> AppointmentsByDay { get; set; } = new();
    public List<AppointmentByStatusDto> AppointmentsByStatus { get; set; } = new();
}

public class AppointmentByDayDto
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public class AppointmentByStatusDto
{
    public AppointmentStatus Status { get; set; }
    public int Count { get; set; }
}

public class MedicalReportDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("patientId")]
    public Guid PatientId { get; set; }

    [JsonPropertyName("patientName")]
    public string PatientName { get; set; } = string.Empty;

    [JsonPropertyName("doctorId")]
    public Guid DoctorId { get; set; }

    [JsonPropertyName("doctorName")]
    public string DoctorName { get; set; } = string.Empty;

    [JsonPropertyName("specialtyName")]
    public string SpecialtyName { get; set; } = string.Empty;

    [JsonPropertyName("examDate")]
    public DateOnly ExamDate { get; set; }

    [JsonPropertyName("symptoms")]
    public string? Symptoms { get; set; }

    [JsonPropertyName("diagnosis")]
    public string? Diagnosis { get; set; }

    [JsonPropertyName("prescription")]
    public string? Prescription { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("examStartTime")]
    public DateTime? ExamStartTime { get; set; }

    [JsonPropertyName("examEndTime")]
    public DateTime? ExamEndTime { get; set; }

    [JsonPropertyName("status")]
    public MedicalReportStatus Status { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class MyQueueStatusDto
{
    [JsonPropertyName("queueTicketId")]
    public Guid QueueTicketId { get; set; }

    [JsonPropertyName("queueNumber")]
    public int QueueNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("doctorName")]
    public string DoctorName { get; set; } = string.Empty;

    [JsonPropertyName("positionInQueue")]
    public int PositionInQueue { get; set; }

    [JsonPropertyName("estimatedWaitMinutes")]
    public int EstimatedWaitMinutes { get; set; }
}

public class QueueTicketDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("queueNumber")]
    public int QueueNumber { get; set; }

    [JsonPropertyName("priority")]
    public bool Priority { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("checkInTime")]
    public DateTime CheckInTime { get; set; }

    [JsonPropertyName("calledAt")]
    public DateTime? CalledAt { get; set; }

    [JsonPropertyName("patientName")]
    public string? PatientName { get; set; }
}

public class DashboardDataDto
{
    public IValueWithChange TotalAppointments { get; set; } = new ValueWithChange<int>();
    public IValueWithChange TotalPatients { get; set; } = new ValueWithChange<int>();
    public IValueWithChange TotalDoctors { get; set; } = new ValueWithChange<int>();
    public IValueWithChange ActiveDoctor { get; set; } = new ValueWithChange<int>();
    public IValueWithChange CompletionRate { get; set; } = new ValueWithChange<double>();
    public IValueWithChange AverageWaitingMinute { get; set; } = new ValueWithChange<double>();
}

public interface IValueWithChange
{
    object Value { get; }
    double Change { get; }
}

public class ValueWithChange<T> : IValueWithChange
{
    public T Value { get; set; } = default!;
    public double Change { get; set; } = 0;

    object IValueWithChange.Value => Value!;
}
