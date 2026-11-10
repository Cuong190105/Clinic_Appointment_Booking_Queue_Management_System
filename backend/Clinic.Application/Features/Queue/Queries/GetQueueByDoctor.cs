using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Queue.Queries
{
    /// <summary>Date = null -> m?c d?nh l?y hàng d?i c?a ngày hôm nay (UTC).</summary>
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
            // "queue.view" (Admin/Receptionist): du?c xem hàng d?i c?a b?t k? bác si nào.
            // N?u không có "queue.view", user ch? du?c xem hàng d?i c?a chính mình
            // (d?a vào "doctor.queue.view", dành cho Doctor).
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
                PatientName = q.PatientName ?? q.Appointment?.Patient?.Person?.FullName,
                DoctorName = q.DoctorName ?? q.Appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName
            }).ToList();
        }
    }
}