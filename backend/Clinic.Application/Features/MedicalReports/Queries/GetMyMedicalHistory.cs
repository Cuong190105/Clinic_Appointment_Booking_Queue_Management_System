using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

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

            // Chỉ hiển thị hồ sơ đã chốt (Finalized) - hồ sơ Draft là bác sĩ đang ghi dở, chưa chính thức.
            return reports
                .Where(r => r.Status == MedicalReportStatus.Finalized)
                .Select(r => new MedicalReportDto
                {
                    Id = r.Id,
                    DoctorName = r.QueueTicket.Appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                    ExamDate = r.QueueTicket.Appointment.WorkSchedule.Date,
                    Symptoms = r.Symptoms,
                    Diagnosis = r.Diagnosis,
                    Prescription = r.Prescription,
                    Notes = r.Notes
                }).ToList();
        }
    }
}