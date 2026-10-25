using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Commands
{
    public record SaveMedicalReportCommand(
        Guid AppointmentId,
        string? Symptoms,
        string? Diagnosis,
        string? Prescription,
        string? Notes,
        bool IsFinalize = false,
        AppointmentReportSnapshot? Appointment = null,
        string? QueueTicketStatus = null,
        DateTime? ExamStartedAt = null
    ) : IRequest<MedicalReportDto>;

    public class SaveMedicalReportHandler : IRequestHandler<SaveMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public SaveMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(SaveMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var appointment = request.Appointment
                ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' not found.");
            if (appointment.Id != request.AppointmentId)
                throw new InvalidOperationException("Appointment snapshot does not match the requested appointment.");

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && appointment.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to record medical report for another doctor's queue.");
            }

            if (request.QueueTicketStatus is not null && !string.Equals(request.QueueTicketStatus, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cannot record medical report for queue ticket with status '{request.QueueTicketStatus}'. Examination must be in progress.");
            }

            var report = await _medicalReportRepository.GetByAppointmentIdAsync(appointment.Id);
            if (report == null)
            {
                report = new MedicalReport(
                    appointment.Id,
                    appointment.PatientId,
                    request.Symptoms,
                    request.Diagnosis,
                    request.Prescription,
                    request.Notes,
                    request.ExamStartedAt ?? DateTime.UtcNow
                );
                report.DoctorId = appointment.DoctorId;
                report.DoctorName = appointment.DoctorName;
                report.SpecialtyName = appointment.SpecialtyName;
                report.ScheduleDate = appointment.Date;
                await _medicalReportRepository.AddAsync(report);
            }
            else
            {
                report.UpdateDetails(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
            }

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
                PatientName = report.Patient?.Person?.FullName ?? appointment.PatientName,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.DoctorName,
                SpecialtyName = appointment.SpecialtyName ?? string.Empty,
                ExamDate = appointment.Date,
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
