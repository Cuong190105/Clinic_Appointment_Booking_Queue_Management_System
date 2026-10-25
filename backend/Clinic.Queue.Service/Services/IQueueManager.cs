using Clinic.Contracts.Events;
using Clinic.Queue.Service.DTOs;
using Clinic.Queue.Service.Enums;

namespace Clinic.Queue.Service.Services;

public interface IQueueManager
{
    Task<QueueTicketDto?> ProcessAppointmentCheckedInAsync(EventEnvelope<AppointmentCheckedIn> envelope, CancellationToken ct = default);
    Task ProcessAppointmentCancelledAsync(EventEnvelope<AppointmentCancelled> envelope, CancellationToken ct = default);
    Task<QueueTicketDto> CallNextAsync(Guid doctorId, DateOnly? date = null, CancellationToken ct = default);
    Task<QueueTicketDto> CallAsync(Guid queueTicketId, CancellationToken ct = default);
    Task StartExamAsync(Guid queueTicketId, Guid? actorDoctorId, bool hasAnyScope, CancellationToken ct = default);
    Task CompleteExamAsync(Guid queueTicketId, Guid? actorDoctorId, bool hasAnyScope, CancellationToken ct = default);
    Task<SkipQueueResultDto> SkipAsync(Guid queueTicketId, QueueStatus? requiredStatus = null, CancellationToken ct = default);
    Task<QueueTicketDto> RetrySkippedAsync(Guid queueTicketId, CancellationToken ct = default);
    Task SetPriorityAsync(Guid queueTicketId, bool priority, CancellationToken ct = default);
    Task<List<QueueTicketDto>> GetQueueByDoctorAsync(Guid doctorId, DateOnly? date = null, CancellationToken ct = default);
    Task<List<MyQueueStatusDto>> GetQueueStatusByPatientAsync(Guid patientId, DateOnly? date = null, CancellationToken ct = default);
    Task<QueueTicketDto?> GetByIdAsync(Guid queueTicketId, CancellationToken ct = default);
    Task<QueueTicketDto?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken ct = default);
    Task<int> GetNextQueueNumberAsync(Guid doctorId, DateOnly queueDate, CancellationToken ct = default);
}
