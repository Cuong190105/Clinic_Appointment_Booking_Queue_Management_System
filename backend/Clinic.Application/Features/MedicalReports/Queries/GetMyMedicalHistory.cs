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
    public record GetMyMedicalHistoryQuery : IRequest<List<MedicalReportDto>>;

    public class GetMyMedicalHistoryHandler : IRequestHandler<GetMyMedicalHistoryQuery, List<MedicalReportDto>>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;

        public GetMyMedicalHistoryHandler(IMedicalReportRepository medicalReportRepository, ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
        }

        public async Task<List<MedicalReportDto>> Handle(GetMyMedicalHistoryQuery request, CancellationToken cancellationToken)
        {
            if (_currentUser.PatientId is null)
            {
                throw new ForbiddenException("Only a patient account has medical history to view.");
            }

            var reports = await _medicalReportRepository.GetHistoryByPatientIdAsync(_currentUser.PatientId.Value);

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
                        PatientId = _currentUser.PatientId.Value,
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
