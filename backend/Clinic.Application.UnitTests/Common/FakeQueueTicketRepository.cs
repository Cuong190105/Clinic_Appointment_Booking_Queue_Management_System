using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Common
{
    public class FakeQueueTicketRepository : IQueueTicketRepository
    {
        private readonly List<QueueTicket> _queueTickets = [];
        public async Task<int> GetNextQueueNumberAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default)
        {
            var maxQueueNumber = _queueTickets
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId && q.CheckInTime.Date == date.Date)
                .Select(q => (int?)q.QueueNumber)
                .Max();

            return (maxQueueNumber ?? 0) + 1;
        }

        public async Task AddAsync(QueueTicket queueTicket)
        {
            _queueTickets.Add(queueTicket);
        }

        public async Task<QueueTicket?> GetByIdAsync(Guid id)
        {
            return _queueTickets.FirstOrDefault(q => q.Id == id);
        }

        public async Task<List<QueueTicket>> GetByDoctorAsync(Guid doctorId, DateTime date)
        {
            return _queueTickets
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId && q.CheckInTime.Date == date.Date)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.QueueNumber)
                .ToList();
        }

        public async Task<QueueTicket?> GetNextWaitingAsync(Guid doctorId, DateTime date)
        {
            return _queueTickets
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId
                    && q.CheckInTime.Date == date.Date
                    && q.Status == QueueStatus.Waiting)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.QueueNumber)
                .FirstOrDefault();
        }

        public async Task UpdateAsync(QueueTicket queueTicket)
        {
            return;
        }

        /// <summary>Giúp test kiểm tra QueueTicket vừa được lưu (số lượng, dữ liệu) mà không cần expose List thô.</summary>
        public IReadOnlyList<QueueTicket> All => _queueTickets;

        public async Task<QueueTicket?> GetActiveTicketAsync(Guid doctorId, DateTime date)
        {
            return _queueTickets
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId
                    && q.CheckInTime.Date == date.Date
                    && (q.Status == QueueStatus.Called || q.Status == QueueStatus.InProgress))
                .FirstOrDefault();
        }

        public async Task<List<QueueTicket>> GetActiveTicketsByPatientAsync(Guid patientId, DateTime date)
        {
            return _queueTickets
                .Where(q => q.Appointment.PatientId == patientId
                    && q.CheckInTime.Date == date.Date
                    && (q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called || q.Status == QueueStatus.InProgress))
                .OrderBy(q => q.CheckInTime)
                .ToList();
        }
    }
}