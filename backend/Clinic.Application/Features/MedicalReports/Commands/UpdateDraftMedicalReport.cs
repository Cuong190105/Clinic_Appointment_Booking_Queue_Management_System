using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Contracts.Events;
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
        private readonly IOutboxWriter? _outboxWriter;
        private readonly IEventContext? _eventContext;

        public UpdateDraftMedicalReportHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork,
            IOutboxWriter? outboxWriter = null,
            IEventContext? eventContext = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
            _outboxWriter = outboxWriter;
            _eventContext = eventContext;
        }

        public async Task<MedicalReportDto> Handle(UpdateDraftMedicalReportCommand request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.ReportId)
                ?? throw new NotFoundException($"Medical report '{request.ReportId}' not found.");

            if (report.Status == MedicalReportStatus.Finalized)
            {
                throw new InvalidOperationException("Hồ sơ đã hoàn tất và chốt, không thể chỉnh sửa.");
            }

            var appointment = report.Appointment;
            var hasAnyScope = _currentUser.HasPermission("queue.complete-exam.any") || _currentUser.HasPermission("medical-report.view.any");
            if (!hasAnyScope && appointment?.WorkSchedule?.DoctorId != _currentUser.DoctorId && appointment?.DoctorId != _currentUser.DoctorId)
            {
                throw new ForbiddenException("You are not allowed to update another doctor's medical report.");
            }

            report.UpdateDetails(request.Symptoms, request.Diagnosis, request.Prescription, request.Notes);

            if (request.IsFinalize)
            {
                report.FinalizeReport();

                var ticket = appointment?.QueueTicket;
                if (ticket != null && ticket.Status == QueueStatus.InProgress)
                {
                    ticket.Complete();
                }

                if (appointment != null && appointment.Status == AppointmentStatus.CheckedIn)
                {
                    appointment.Complete(_currentUser.UserId ?? Guid.Empty);
                }

                if (appointment != null && _outboxWriter != null)
                {
                    var completedAt = DateTimeOffset.UtcNow;
                    var docId = appointment.WorkSchedule?.DoctorId ?? appointment.DoctorId;
                    _outboxWriter.Add(
                        new EventEnvelope<ExamCompleted>
                        {
                            EventId = Guid.NewGuid(),
                            EventType = EventTypes.ExamCompleted,
                            SchemaVersion = 1,
                            OccurredAt = completedAt,
                            CorrelationId = _eventContext?.CorrelationId ?? Guid.NewGuid(),
                            CausationId = _eventContext?.CausationId,
                            Producer = "queue-service",
                            Payload = new ExamCompleted(
                                ticket?.Id ?? Guid.Empty,
                                appointment.Id,
                                docId,
                                completedAt)
                        },
                        "queue.exam-completed");
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = report.AppointmentId,
                PatientId = report.PatientId,
                PatientName = report.Patient?.Person?.FullName ?? appointment?.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = appointment?.WorkSchedule?.DoctorId ?? appointment?.DoctorId ?? Guid.Empty,
                DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? appointment?.DoctorName ?? string.Empty,
                ExamDate = appointment?.WorkSchedule?.Date ?? appointment?.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt),
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

