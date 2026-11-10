using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Commands
{
    public record FinalizeMedicalReportCommand(Guid MedicalReportId) : IRequest<MedicalReportDto>;

    public class FinalizeMedicalReportHandler : IRequestHandler<FinalizeMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork? _unitOfWork;

        public FinalizeMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(FinalizeMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.MedicalReportId)
                ?? throw new NotFoundException($"Medical report '{request.MedicalReportId}' not found.");

            var appointment = report.Appointment ?? report.QueueTicket?.Appointment;
            var doctorId = appointment?.WorkSchedule?.DoctorId;
            var isAssignedDoctor = doctorId.HasValue && doctorId == _currentUser.DoctorId;
            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any")
                || _currentUser.HasPermission("medical-report.view.any")
                || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !hasAnyScope)
            {
                throw new ForbiddenException("You are not allowed to finalize medical report for another doctor's appointment.");
            }

            if (report.Status != MedicalReportStatus.Finalized)
            {
                report.FinalizeReport();
                await _medicalReportRepository.AddAuditAsync(new MedicalAccessAudit(
                    report.Id,
                    _currentUser.UserId ?? Guid.Empty,
                    "Finalize",
                    $"Finalized medical report {report.Id}"
                ));
                if (_unitOfWork != null)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
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
                AppointmentId = appointment?.Id ?? report.AppointmentId,
                QueueTicketId = report.QueueTicketId ?? appointment?.QueueTicket?.Id,
                PatientId = appointment?.PatientId ?? Guid.Empty,
                PatientName = appointment?.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = doctorId ?? Guid.Empty,
                DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                SpecialtyName = string.Empty,
                ExamDate = appointment?.WorkSchedule?.Date ?? DateOnly.MinValue,
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
