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
    public record SaveMedicalReportCommand(
        Guid? AppointmentId = null,
        Guid? QueueTicketId = null,
        string? Symptoms = null,
        string? Diagnosis = null,
        string? Prescription = null,
        string? Notes = null,
        bool IsFinalize = false,
        Guid? Id = null
    ) : IRequest<MedicalReportDto>
    {
        // Legacy constructor for backward compatibility with existing unit tests
        public SaveMedicalReportCommand(
            Guid queueTicketId,
            string? symptoms,
            string? diagnosis,
            string? prescription,
            string? notes,
            bool isFinalize = false)
            : this(null, queueTicketId, symptoms, diagnosis, prescription, notes, isFinalize, null)
        {
        }
    }

    public class SaveMedicalReportHandler : IRequestHandler<SaveMedicalReportCommand, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IAppointmentRepository? _appointmentRepository;
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork? _unitOfWork;

        public SaveMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
            : this(medicalReportRepository, null, queueTicketRepository, currentUser, unitOfWork)
        {
        }

        public SaveMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            IAppointmentRepository? appointmentRepository,
            IQueueTicketRepository queueTicketRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _appointmentRepository = appointmentRepository;
            _queueTicketRepository = queueTicketRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(SaveMedicalReportCommand request, CancellationToken cancellationToken)
        {
            MedicalReport? report = null;
            Appointment? appointment = null;

            if (request.Id.HasValue && request.Id.Value != Guid.Empty)
            {
                report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.Id.Value)
                    ?? throw new NotFoundException($"Medical report '{request.Id.Value}' not found.");
                appointment = report.Appointment ?? report.QueueTicket?.Appointment;
                if (appointment == null && _appointmentRepository != null && report.AppointmentId != Guid.Empty)
                {
                    appointment = await _appointmentRepository.GetByIdAsync(report.AppointmentId);
                }
            }
            else if (request.AppointmentId.HasValue && request.AppointmentId.Value != Guid.Empty)
            {
                if (_appointmentRepository != null)
                {
                    appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId.Value);
                }

                // Fallback: check queue ticket repository if not found in appointment repo (e.g. legacy ticket id passed as first arg)
                if (appointment == null)
                {
                    var ticket = await _queueTicketRepository.GetByIdAsync(request.AppointmentId.Value);
                    if (ticket != null)
                    {
                        appointment = ticket.Appointment;
                    }
                }

                if (appointment == null)
                {
                    throw new NotFoundException($"Appointment '{request.AppointmentId.Value}' not found.");
                }
            }
            else if (request.QueueTicketId.HasValue && request.QueueTicketId.Value != Guid.Empty)
            {
                var ticket = await _queueTicketRepository.GetByIdAsync(request.QueueTicketId.Value)
                    ?? throw new NotFoundException($"Queue ticket '{request.QueueTicketId.Value}' not found.");
                appointment = ticket.Appointment;
            }
            else
            {
                throw new ArgumentException("AppointmentId or QueueTicketId must be provided.");
            }

            var doctorId = appointment?.WorkSchedule?.DoctorId;
            var isAssignedDoctor = doctorId.HasValue && doctorId == _currentUser.DoctorId;
            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any")
                || _currentUser.HasPermission("medical-report.view.any")
                || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !hasAnyScope)
            {
                throw new ForbiddenException("You are not allowed to record medical report for another doctor's appointment.");
            }

            if (report == null && appointment != null)
            {
                report = await _medicalReportRepository.GetByAppointmentIdAsync(appointment.Id);
                if (report == null && request.AppointmentId.HasValue)
                {
                    report = await _medicalReportRepository.GetByAppointmentIdAsync(request.AppointmentId.Value);
                }
                if (report == null)
                {
                    var lookupTicketId = request.QueueTicketId ?? (appointment.QueueTicket?.Id ?? request.AppointmentId);
                    if (lookupTicketId.HasValue)
                    {
                        report = await _medicalReportRepository.GetByQueueTicketIdAsync(lookupTicketId.Value);
                    }
                }
            }

            if (report == null)
            {
                report = new MedicalReport(
                    appointment.Id,
                    request.Symptoms,
                    request.Diagnosis,
                    request.Prescription,
                    request.Notes,
                    DateTime.UtcNow
                );
                if (appointment.QueueTicket != null)
                {
                    report.QueueTicketId = appointment.QueueTicket.Id;
                }
                else if (request.QueueTicketId.HasValue)
                {
                    report.QueueTicketId = request.QueueTicketId.Value;
                }

                await _medicalReportRepository.AddAsync(report);
                await _medicalReportRepository.AddAuditAsync(new MedicalAccessAudit(
                    report.Id,
                    _currentUser.UserId ?? Guid.Empty,
                    "CreateDraft",
                    $"Created draft medical report for appointment {appointment.Id}"
                ));
            }
            else
            {
                if (report.Status == MedicalReportStatus.Finalized)
                {
                    throw new InvalidOperationException("Cannot update a finalized medical report. Use amendments instead.");
                }

                report.UpdateDetails(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);
                await _medicalReportRepository.AddAuditAsync(new MedicalAccessAudit(
                    report.Id,
                    _currentUser.UserId ?? Guid.Empty,
                    "UpdateDraft",
                    $"Updated draft medical report for appointment {appointment.Id}"
                ));
            }

            if (request.IsFinalize)
            {
                report.FinalizeReport();
                await _medicalReportRepository.AddAuditAsync(new MedicalAccessAudit(
                    report.Id,
                    _currentUser.UserId ?? Guid.Empty,
                    "Finalize",
                    $"Finalized medical report for appointment {appointment.Id}"
                ));
            }

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
