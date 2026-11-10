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
    public record GetMedicalReportByAppointmentQuery(Guid AppointmentId) : IRequest<MedicalReportDto?>;

    public class GetMedicalReportByAppointmentHandler : IRequestHandler<GetMedicalReportByAppointmentQuery, MedicalReportDto?>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public GetMedicalReportByAppointmentHandler(
            IMedicalReportRepository medicalReportRepository,
            IAppointmentRepository appointmentRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto?> Handle(GetMedicalReportByAppointmentQuery request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId)
                ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' not found.");

            var isAssignedDoctor = appointment.WorkSchedule?.DoctorId == _currentUser.DoctorId;
            var isOwnerPatient = appointment.PatientId == _currentUser.PatientId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any") || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
            {
                throw new ForbiddenException("You do not have permission to view this medical report.");
            }

            var report = await _medicalReportRepository.GetByAppointmentIdAsync(request.AppointmentId);
            if (report == null)
            {
                return null;
            }

            var audit = new MedicalAccessAudit(
                report.Id,
                _currentUser.UserId ?? Guid.Empty,
                "View",
                $"Viewed medical report for appointment {request.AppointmentId}"
            );
            await _medicalReportRepository.AddAuditAsync(audit);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
                QueueTicketId = report.QueueTicketId ?? appointment.QueueTicket?.Id,
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
