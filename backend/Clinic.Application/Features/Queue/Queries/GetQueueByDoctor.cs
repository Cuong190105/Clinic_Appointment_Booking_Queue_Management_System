using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Queue.Queries
{
    /// <summary>Date = null -> mặc định lấy hàng đợi của ngày hôm nay (UTC).</summary>
    public record GetQueueByDoctorQuery(Guid DoctorId, DateTime? Date = null) : IRequest<List<QueueTicketDto>>;

    public class GetQueueByDoctorHandler : IRequestHandler<GetQueueByDoctorQuery, List<QueueTicketDto>>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;

        public GetQueueByDoctorHandler(
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
        }

        public async Task<List<QueueTicketDto>> Handle(GetQueueByDoctorQuery query, CancellationToken cancellationToken)
        {
            // "queue.view" (Admin/Receptionist): được xem hàng đợi của bất kỳ bác sĩ nào.
            // Nếu không có "queue.view", user chỉ được xem hàng đợi của chính mình
            // (dựa vào "doctor.queue.view", dành cho Doctor).
            var hasAnyScope = _currentUser.HasPermission("queue.view");
            if (!hasAnyScope && query.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to view this doctor's queue.");
            }

            var date = query.Date ?? DateTime.UtcNow;

            var queueTickets = await _queueTicketRepository.GetByDoctorAsync(query.DoctorId, date);

            return queueTickets.Select(q => new QueueTicketDto
            {
                Id = q.Id,
                AppointmentId = q.AppointmentId,
                QueueNumber = q.QueueNumber,
                Priority = q.Priority,
                Status = q.Status.ToString(),
                CheckInTime = q.CheckInTime,
                CalledAt = q.CalledAt,
                PatientName = q.Appointment.Patient?.Person?.FullName
            }).ToList();
        }
    }
}