using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.MedicalReports.Queries;

public record GetMyMedicalHistoryQuery : IRequest<List<MedicalReportDto>>;

public sealed class GetMyMedicalHistoryHandler : IRequestHandler<GetMyMedicalHistoryQuery, List<MedicalReportDto>>
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
            throw new ForbiddenException("Only a patient account has medical history to view.");

        var reports = await _medicalReportRepository.GetHistoryByPatientIdAsync(_currentUser.PatientId.Value);
        return reports
            .Where(report => report.Status == MedicalReportStatus.Finalized)
            .Select(report => new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = report.AppointmentId,
                PatientId = report.PatientId,
                PatientName = report.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = report.DoctorId ?? Guid.Empty,
                DoctorName = report.DoctorName ?? string.Empty,
                SpecialtyName = report.SpecialtyName ?? string.Empty,
                ExamDate = report.ScheduleDate ?? DateOnly.FromDateTime(report.CreatedAt),
                Symptoms = report.Symptoms,
                Diagnosis = report.Diagnosis,
                Prescription = report.Prescription,
                Notes = report.Notes,
                ExamStartTime = report.ExamStartTime,
                ExamEndTime = report.ExamEndTime,
                Status = report.Status,
                CreatedAt = report.CreatedAt
            }).ToList();
    }
}
