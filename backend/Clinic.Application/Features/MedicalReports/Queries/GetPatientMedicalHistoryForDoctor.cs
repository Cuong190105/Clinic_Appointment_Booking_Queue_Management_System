using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Queries
{
    public record GetPatientMedicalHistoryForDoctorQuery(Guid PatientId) : IRequest<List<MedicalReportDto>>;

    public class GetPatientMedicalHistoryForDoctorHandler : IRequestHandler<GetPatientMedicalHistoryForDoctorQuery, List<MedicalReportDto>>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork? _unitOfWork;

        public GetPatientMedicalHistoryForDoctorHandler(
            IMedicalReportRepository medicalReportRepository,
            IAppointmentRepository appointmentRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<MedicalReportDto>> Handle(GetPatientMedicalHistoryForDoctorQuery request, CancellationToken cancellationToken)
        {
            var hasAnyScope = _currentUser.HasPermission("patient-history.view.any") || _currentUser.IsInRole("Admin");

            if (!hasAnyScope)
            {
                if (_currentUser.DoctorId == null)
                {
                    throw new ForbiddenException("Only a doctor or admin can view patient medical history.");
                }

                var isRelated = await _appointmentRepository.ExistsForDoctorAndPatientAsync(
                    _currentUser.DoctorId.Value, request.PatientId);

                if (!isRelated)
                {
                    throw new ForbiddenException("You can only view medical history of patients who have appointments with you.");
                }
            }

            var reports = await _medicalReportRepository.GetHistoryByPatientIdAsync(request.PatientId);

            foreach (var r in reports)
            {
                await _medicalReportRepository.AddAuditAsync(new MedicalAccessAudit(
                    r.Id,
                    _currentUser.UserId ?? Guid.Empty,
                    "ViewHistory",
                    $"Doctor viewed patient {request.PatientId} history entry {r.Id}"
                ));
            }
            if (reports.Any() && _unitOfWork != null)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return reports
                .Where(r => r.Status == MedicalReportStatus.Finalized)
                .Select(r =>
                {
                    var appointment = r.Appointment ?? r.QueueTicket?.Appointment;
                    var amendments = (r.Amendments ?? Enumerable.Empty<MedicalReportAmendment>())
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
                        Id = r.Id,
                        AppointmentId = appointment?.Id ?? r.AppointmentId,
                        QueueTicketId = r.QueueTicketId ?? appointment?.QueueTicket?.Id,
                        PatientId = request.PatientId,
                        PatientName = appointment?.Patient?.Person?.FullName ?? string.Empty,
                        DoctorId = appointment?.WorkSchedule?.DoctorId ?? Guid.Empty,
                        DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                        SpecialtyName = string.Empty,
                        ExamDate = appointment?.WorkSchedule?.Date ?? DateOnly.MinValue,
                        Symptoms = r.Symptoms,
                        Diagnosis = r.Diagnosis,
                        Prescription = r.Prescription,
                        Notes = r.Notes,
                        ExamStartTime = r.ExamStartTime,
                        ExamEndTime = r.ExamEndTime,
                        Status = r.Status,
                        CreatedAt = r.CreatedAt,
                        Amendments = amendments
                    };
                }).ToList();
        }
    }
}
