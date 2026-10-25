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
    public record FinalizeMedicalReportCommand(Guid MedicalReportId) : IRequest<MedicalReportDto>;

    public class FinalizeMedicalReportHandler : IRequestHandler<FinalizeMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public FinalizeMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(FinalizeMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.MedicalReportId)
                ?? throw new NotFoundException($"Medical report '{request.MedicalReportId}' not found.");

            if (report.Status == MedicalReportStatus.Finalized)
            {
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

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && report.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to finalize medical report for another doctor's queue.");
            }

            report.FinalizeReport();

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
