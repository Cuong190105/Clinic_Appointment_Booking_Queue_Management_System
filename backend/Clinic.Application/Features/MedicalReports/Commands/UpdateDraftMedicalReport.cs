using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Commands
{
    public record UpdateDraftMedicalReportCommand(
        Guid ReportId,
        string? Symptoms,
        string? Diagnosis,
        string? Prescription,
        string? Notes,
        bool IsFinalize = false
    ) : IRequest<MedicalReportDto>;

    public class UpdateDraftMedicalReportHandler : IRequestHandler<UpdateDraftMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateDraftMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(UpdateDraftMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.ReportId)
                ?? throw new NotFoundException($"Medical report '{request.ReportId}' not found.");

            if (report.Status == MedicalReportStatus.Finalized)
            {
                throw new InvalidOperationException("Hồ sơ đã hoàn tất và chốt, không thể chỉnh sửa.");
            }

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && report.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to update another doctor's medical report.");
            }

            report.UpdateDetails(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);

            if (request.IsFinalize)
            {
                report.FinalizeReport();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = report.AppointmentId,
                PatientId = report.PatientId,
                PatientName = report.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = report.DoctorId ?? Guid.Empty,
                DoctorName = report.DoctorName ?? string.Empty,
                SpecialtyName = report.SpecialtyName ?? string.Empty,
                ExamDate = report.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt),
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

