using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
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

        public GetPatientMedicalHistoryForDoctorHandler(
            IMedicalReportRepository medicalReportRepository,
            IAppointmentRepository appointmentRepository,
            ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
        }

        public async Task<List<MedicalReportDto>> Handle(GetPatientMedicalHistoryForDoctorQuery request, CancellationToken cancellationToken)
        {
            var hasAnyScope = _currentUser.HasPermission("patient-history.view.any");

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

            return reports
                .Where(r => r.Status == MedicalReportStatus.Finalized)
                .Select(r => new MedicalReportDto
                {
                    Id = r.Id,
                    QueueTicketId = r.QueueTicketId,
                    PatientId = request.PatientId,
                    PatientName = r.QueueTicket.Appointment.Patient?.Person?.FullName ?? string.Empty,
                    DoctorId = r.QueueTicket.Appointment.WorkSchedule.DoctorId,
                    DoctorName = r.QueueTicket.Appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    ExamDate = r.QueueTicket.Appointment.WorkSchedule.Date,
                    Symptoms = r.Symptoms,
                    Diagnosis = r.Diagnosis,
                    Prescription = r.Prescription,
                    Notes = r.Notes,
                    ExamStartTime = r.ExamStartTime,
                    ExamEndTime = r.ExamEndTime,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt
                }).ToList();
        }
    }
}
