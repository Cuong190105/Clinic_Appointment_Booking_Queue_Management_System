using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Linq;
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
        private readonly IUnitOfWork? _unitOfWork;

        public GetMedicalReportByTicketHandler(
            IMedicalReportRepository medicalReportRepository,
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto?> Handle(GetMedicalReportByTicketQuery request, CancellationToken cancellationToken)
        {
            var ticket = await _queueTicketRepository.GetByIdAsync(request.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{request.QueueTicketId}' not found.");

            var appointment = ticket.Appointment;
            var isAssignedDoctor = appointment?.WorkSchedule?.DoctorId == _currentUser.DoctorId;
            var isOwnerPatient = appointment?.PatientId == _currentUser.PatientId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any") || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
            {
                throw new ForbiddenException("You do not have permission to view this medical report.");
            }

            var report = await _medicalReportRepository.GetByAppointmentIdAsync(appointment.Id);
            if (report == null)
            {
                report = await _medicalReportRepository.GetByQueueTicketIdAsync(request.QueueTicketId);
            }

            if (report == null) return null;

            var audit = new MedicalAccessAudit(
                report.Id,
                _currentUser.UserId ?? Guid.Empty,
                "View",
                $"Viewed medical report for ticket {request.QueueTicketId}"
            );
            await _medicalReportRepository.AddAuditAsync(audit);
            if (_unitOfWork != null)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var amendments = (report.Amendments ?? Enumerable.Empty<MedicalReportAmendment>())
                .Select(a => new MedicalReportAmendmentDto
                {
                    Id = a.Id,
                    MedicalReportId = a.MedicalReportId,
                    Reason = a.Reason,
                    Content = a.Content,
                    CreatedByDoctorId = a.CreatedByDoctorId,
                    DoctorName = a.DoctorName,
                    CreatedAt = a.CreatedAt
                }).ToList();

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = appointment.Id,
                QueueTicketId = report.QueueTicketId ?? ticket.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = appointment.WorkSchedule?.DoctorId ?? Guid.Empty,
                DoctorName = appointment.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                SpecialtyName = string.Empty,
                ExamDate = appointment.WorkSchedule?.Date ?? DateOnly.MinValue,
                Symptoms = report.Symptoms,
                Diagnosis = report.Diagnosis,
                Prescription = report.Prescription,
                Notes = report.Notes,
                ExamStartTime = report.ExamStartTime,
                ExamEndTime = report.ExamEndTime,
                Status = report.Status,
                CreatedAt = report.CreatedAt,
                Amendments = amendments
            };
        }
    }
}
