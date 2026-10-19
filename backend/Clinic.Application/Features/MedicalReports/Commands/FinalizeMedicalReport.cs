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

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            var appointment = report.Appointment;
            var ticket = appointment.QueueTicket
                ?? throw new InvalidOperationException("The appointment has no queue ticket to finalize.");
            if (!hasAnyScope && appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to finalize medical report for another doctor's queue.");
            }

            if (ticket.Status != QueueStatus.InProgress)
            {
                throw new InvalidOperationException($"Cannot finalize medical report for queue ticket with status '{ticket.Status}'. Examination must be in progress.");
            }

            report.FinalizeReport();
            ticket.Complete();

            if (appointment.Status == AppointmentStatus.CheckedIn)
            {
                appointment.Complete(_currentUser.UserId ?? throw new UnauthorizedAccessException());
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
