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
        Guid QueueTicketId,
        string? Symptoms,
        string? Diagnosis,
        string? Prescription,
        string? Notes,
        bool IsFinalize = false
    ) : IRequest<MedicalReportDto>;

    public class SaveMedicalReportHandler : IRequestHandler<SaveMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public SaveMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(SaveMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var ticket = await _queueTicketRepository.GetByIdAsync(request.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{request.QueueTicketId}' not found.");

            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && ticket.Appointment.WorkSchedule.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to record medical report for another doctor's queue.");
            }

            if (ticket.Status != QueueStatus.InProgress)
            {
                throw new InvalidOperationException($"Cannot record medical report for queue ticket with status '{ticket.Status}'. Examination must be in progress.");
            }

            var report = await _medicalReportRepository.GetByQueueTicketIdAsync(request.QueueTicketId);
            if (report == null)
            {
                report = new MedicalReport(
                    ticket.Id,
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

                if (ticket.Appointment.Status == AppointmentStatus.CheckedIn)
                {
                    ticket.Appointment.Complete(_currentUser.UserId ?? throw new UnauthorizedAccessException());
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

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
