using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Queue.Queries
{
    /// <summary>
    /// Tr? v? danh sách r?ng n?u b?nh nhân chua check-in trong ngày hôm nay (không có vé nào dang
    /// "s?ng"). Có th? tr? v? NHI?U ph?n t? n?u b?nh nhân dã check-in nhi?u hon 1 l?ch h?n trong
    /// cùng ngày (vd. khám 2 chuyên khoa khác nhau) - m?i vé active là 1 ph?n t? riêng, không du?c
    /// g?p/rút g?n v? 1 vé "m?i nh?t".
    /// </summary>
    public record GetMyQueueStatusQuery : IRequest<List<MyQueueStatusDto>>;

    public class GetMyQueueStatusHandler : IRequestHandler<GetMyQueueStatusQuery, List<MyQueueStatusDto>>
    {
        // Th?i gian khám trung bình u?c lu?ng - dùng t?m cho t?i khi làm thu?t toán u?c tính
        // d?a trên l?ch s? th?i gian khám th?c t? (m?c VIII - tính nang nâng cao, tu? ch?n).
        private const int EstimatedMinutesPerPatient = 10;

        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;

        public GetMyQueueStatusHandler(IQueueTicketRepository queueTicketRepository, ICurrentUser currentUser)
        {
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
        }

        public async Task<List<MyQueueStatusDto>> Handle(GetMyQueueStatusQuery request, CancellationToken cancellationToken)
        {
            if (_currentUser.PatientId is null)
            {
                throw new ForbiddenException("Only a patient account has a queue status to view.");
            }

            var today = DateTime.UtcNow;

            var myTickets = await _queueTicketRepository.GetActiveTicketsByPatientAsync(_currentUser.PatientId.Value, today);
            if (myTickets.Count == 0)
            {
                return [];
            }

            var result = new List<MyQueueStatusDto>(myTickets.Count);

            // Các vé có th? thu?c nhi?u bác si khác nhau (b?nh nhân check-in nhi?u l?ch h?n trong
            // cùng ngày) -> cache hàng d?i theo t?ng bác si d? tránh query l?p l?i cùng 1 bác si.
            var queueByDoctor = new Dictionary<Guid, List<QueueTicket>>();

            foreach (var myTicket in myTickets)
            {
                var doctorId = myTicket.Appointment.WorkSchedule.DoctorId;

                if (!queueByDoctor.TryGetValue(doctorId, out var dayQueue))
                {
                    dayQueue = await _queueTicketRepository.GetByDoctorAsync(doctorId, today);
                    queueByDoctor[doctorId] = dayQueue;
                }

                // dayQueue dã du?c s?p theo Priority (uu tiên tru?c) r?i QueueNumber tang d?n -
                // dúng th? t? s? du?c g?i. Ð?m s? vé dang ch?/dã g?i d?ng tru?c vé c?a mình.
                var waitingAhead = 0;
                foreach (var ticket in dayQueue)
                {
                    if (ticket.Id == myTicket.Id)
                    {
                        break;
                    }

                    if (ticket.Status == QueueStatus.Waiting || ticket.Status == QueueStatus.Called)
                    {
                        waitingAhead++;
                    }
                }

                result.Add(new MyQueueStatusDto
                {
                    QueueTicketId = myTicket.Id,
                    QueueNumber = myTicket.QueueNumber,
                    Status = myTicket.Status.ToString(),
                    DoctorName = myTicket.Appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    PositionInQueue = waitingAhead + 1,
                    EstimatedWaitMinutes = waitingAhead * EstimatedMinutesPerPatient
                });
            }

            return result;
        }
    }
}