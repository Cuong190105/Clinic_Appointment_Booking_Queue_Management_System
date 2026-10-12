using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Queries
{
    public record GetMedicalReportByTicketQuery(Guid QueueTicketId) : IRequest<MedicalReportDto?>;

    public class GetMedicalReportByTicketHandler : IRequestHandler<GetMedicalReportByTicketQuery, MedicalReportDto?>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;

        public GetMedicalReportByTicketHandler(
            IMedicalReportRepository medicalReportRepository,
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
        }

        public async Task<MedicalReportDto?> Handle(GetMedicalReportByTicketQuery request, CancellationToken cancellationToken)
        {
            var ticket = await _queueTicketRepository.GetByIdAsync(request.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{request.QueueTicketId}' not found.");

            var isAssignedDoctor = ticket.Appointment.WorkSchedule.DoctorId == _currentUser.DoctorId;
            var isOwnerPatient = ticket.Appointment.PatientId == _currentUser.PatientId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any");

            if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
            {
                throw new ForbiddenException("You do not have permission to view this medical report.");
            }

            var report = await _medicalReportRepository.GetByQueueTicketIdAsync(request.QueueTicketId);
            if (report == null) return null;

            return new MedicalReportDto
            {
                Id = report.Id,
                QueueTicketId = report.QueueTicketId,
                PatientId = ticket.Appointment.PatientId,
                PatientName = ticket.Appointment.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = ticket.Appointment.WorkSchedule.DoctorId,
                DoctorName = ticket.Appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                ExamDate = ticket.Appointment.WorkSchedule.Date,
                Symptoms = report.Symptoms,
                Diagnosis = report.Diagnosis,
                Prescription = report.Prescription,
                Notes = report.Notes,
                ExamStartTime = report.ExamStartTime,
                ExamEndTime = report.ExamEndTime,
                Status = report.Status,
                CreatedAt = report.CreatedAt
            };
        }
    }
}
