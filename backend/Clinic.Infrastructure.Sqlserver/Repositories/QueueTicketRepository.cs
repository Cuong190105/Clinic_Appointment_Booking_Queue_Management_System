using System.Data;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class QueueTicketRepository : IQueueTicketRepository
    {
        private readonly ApplicationDbContext _context;

        public QueueTicketRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetNextQueueNumberAsync(Guid doctorId, DateTime date, CancellationToken cancellationToken = default)
        {
            var day = DateOnly.FromDateTime(date.Date);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = @"
                MERGE INTO QueueCounters WITH (HOLDLOCK) AS target
                USING (SELECT @DoctorId AS DoctorId, @Date AS [Date]) AS source
                    ON target.DoctorId = source.DoctorId AND target.[Date] = source.[Date]
                WHEN MATCHED THEN
                    UPDATE SET CurrentNumber = target.CurrentNumber + 1
                WHEN NOT MATCHED THEN
                    INSERT (DoctorId, [Date], CurrentNumber)
                    VALUES (source.DoctorId, source.[Date], 1)
                OUTPUT INSERTED.CurrentNumber;";

            var pDoctorId = command.CreateParameter();
            pDoctorId.ParameterName = "@DoctorId";
            pDoctorId.Value = doctorId;
            command.Parameters.Add(pDoctorId);

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@Date";
            pDate.Value = day.ToDateTime(TimeOnly.MinValue);
            pDate.DbType = DbType.Date;
            command.Parameters.Add(pDate);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }

        public async Task AddAsync(QueueTicket queueTicket)
        {
            await _context.QueueTickets.AddAsync(queueTicket);
        }

        public async Task<QueueTicket?> GetByIdAsync(Guid id)
        {
            return await _context.QueueTickets
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<List<QueueTicket>> GetByDoctorAsync(Guid doctorId, DateTime date)
        {
            return await _context.QueueTickets
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(w => w.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId && q.CheckInTime.Date == date.Date)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.QueueNumber)
                .ToListAsync();
        }

        public async Task<List<QueueTicket>> GetActiveTicketsByPatientAsync(Guid patientId, DateTime date)
        {
            return await _context.QueueTickets
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                        .ThenInclude(w => w.Doctor)
                            .ThenInclude(d => d.Employee)
                                .ThenInclude(e => e.Person)
                .Where(q => q.Appointment.PatientId == patientId
                    && q.CheckInTime.Date == date.Date
                    && (q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called || q.Status == QueueStatus.InProgress))
                .OrderBy(q => q.CheckInTime)
                .ToListAsync();
        }

        public async Task<QueueTicket?> GetNextWaitingAsync(Guid doctorId, DateTime date)
        {
            return await _context.QueueTickets
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId
                    && q.CheckInTime.Date == date.Date
                    && q.Status == QueueStatus.Waiting)
                .OrderByDescending(q => q.Priority)
                .ThenBy(q => q.QueueNumber)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(QueueTicket queueTicket)
        {
            _context.QueueTickets.Update(queueTicket);
        }

        public async Task<QueueTicket?> GetActiveTicketAsync(Guid doctorId, DateTime date)
        {
            return await _context.QueueTickets
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.WorkSchedule)
                .Include(q => q.Appointment)
                    .ThenInclude(a => a.Patient)
                        .ThenInclude(p => p.Person)
                .Where(q => q.Appointment.WorkSchedule.DoctorId == doctorId
                    && q.CheckInTime.Date == date.Date
                    && (q.Status == QueueStatus.Called || q.Status == QueueStatus.InProgress))
                .FirstOrDefaultAsync();
        }
    }
}