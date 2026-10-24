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
                    PatientName = report.Patient?.Person?.FullName ?? report.Appointment?.Patient?.Person?.FullName ?? string.Empty,
                    DoctorId = report.Appointment?.DoctorId ?? Guid.Empty,
                    DoctorName = report.Appointment?.DoctorName ?? string.Empty,
                    ExamDate = report.Appointment?.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt),
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
            var appointment = report.Appointment;
            var ticket = appointment?.QueueTicket;
            if (!hasAnyScope && appointment?.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to finalize medical report for another doctor's queue.");
            }

            if (ticket != null && ticket.Status != QueueStatus.InProgress)
            {
                throw new InvalidOperationException($"Cannot finalize medical report for queue ticket with status '{ticket.Status}'. Examination must be in progress.");
            }

            report.FinalizeReport();
            ticket?.Complete();

            if (appointment != null && appointment.Status == AppointmentStatus.CheckedIn)
            {
                appointment.Complete(_currentUser.UserId ?? Guid.Empty);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = report.AppointmentId,
                PatientId = report.PatientId,
                PatientName = report.Patient?.Person?.FullName ?? appointment?.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = appointment?.DoctorId ?? Guid.Empty,
                DoctorName = appointment?.DoctorName ?? string.Empty,
                ExamDate = appointment?.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt),
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
