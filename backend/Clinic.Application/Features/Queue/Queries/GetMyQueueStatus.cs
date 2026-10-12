using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Queue.Queries
{
    /// <summary>
    /// Trả về danh sách rỗng nếu bệnh nhân chưa check-in trong ngày hôm nay (không có vé nào đang
    /// "sống"). Có thể trả về NHIỀU phần tử nếu bệnh nhân đã check-in nhiều hơn 1 lịch hẹn trong
    /// cùng ngày (vd. khám 2 chuyên khoa khác nhau) - mỗi vé active là 1 phần tử riêng, không được
    /// gộp/rút gọn về 1 vé "mới nhất".
    /// </summary>
    public record GetMyQueueStatusQuery : IRequest<List<MyQueueStatusDto>>;

    public class GetMyQueueStatusHandler : IRequestHandler<GetMyQueueStatusQuery, List<MyQueueStatusDto>>
    {
        // Thời gian khám trung bình ước lượng - dùng tạm cho tới khi làm thuật toán ước tính
        // dựa trên lịch sử thời gian khám thực tế (mục VIII - tính năng nâng cao, tuỳ chọn).
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

            // Các vé có thể thuộc nhiều bác sĩ khác nhau (bệnh nhân check-in nhiều lịch hẹn trong
            // cùng ngày) -> cache hàng đợi theo từng bác sĩ để tránh query lặp lại cùng 1 bác sĩ.
            var queueByDoctor = new Dictionary<Guid, List<QueueTicket>>();

            foreach (var myTicket in myTickets)
            {
                var doctorId = myTicket.Appointment.WorkSchedule.DoctorId;

                if (!queueByDoctor.TryGetValue(doctorId, out var dayQueue))
                {
                    dayQueue = await _queueTicketRepository.GetByDoctorAsync(doctorId, today);
                    queueByDoctor[doctorId] = dayQueue;
                }

                // dayQueue đã được sắp theo Priority (ưu tiên trước) rồi QueueNumber tăng dần -
                // đúng thứ tự sẽ được gọi. Đếm số vé đang chờ/đã gọi đứng trước vé của mình.
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