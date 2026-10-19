using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
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
        bool IsFinalize = false
    ) : IRequest<MedicalReportDto>;

    public class SaveMedicalReportHandler : IRequestHandler<SaveMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public SaveMedicalReportHandler(
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

        public async Task<MedicalReportDto> Handle(SaveMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId)
                ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' not found.");
            var ticket = appointment.QueueTicket
                ?? throw new InvalidOperationException("The appointment has not been checked in.");

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to record medical report for another doctor's queue.");
            }

            if (ticket.Status != QueueStatus.InProgress)
            {
                throw new InvalidOperationException($"Cannot record medical report for queue ticket with status '{ticket.Status}'. Examination must be in progress.");
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
                    ticket.CalledAt ?? DateTime.UtcNow
                );
                await _medicalReportRepository.AddAsync(report);
            }
            else
            {
                report.UpdateDetails(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
            }

            if (request.IsFinalize)
            {
                report.FinalizeReport();
                ticket.Complete();

                if (appointment.Status == AppointmentStatus.CheckedIn)
                {
                    appointment.Complete(_currentUser.UserId ?? throw new UnauthorizedAccessException());
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = report.AppointmentId,
                PatientId = report.PatientId,
                PatientName = report.Patient?.Person?.FullName ?? appointment.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = appointment.WorkSchedule.DoctorId,
                DoctorName = appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                ExamDate = appointment.WorkSchedule.Date,
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
