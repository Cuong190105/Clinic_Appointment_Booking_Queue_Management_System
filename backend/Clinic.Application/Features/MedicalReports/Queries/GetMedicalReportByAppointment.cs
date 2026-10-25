using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.MedicalReports.Queries;

public record GetMedicalReportByAppointmentQuery(Guid AppointmentId) : IRequest<MedicalReportDto?>;

public sealed class GetMedicalReportByAppointmentHandler
    : IRequestHandler<GetMedicalReportByAppointmentQuery, MedicalReportDto?>
{
    private readonly IMedicalReportRepository _medicalReportRepository;
    private readonly ICurrentUser _currentUser;

    public GetMedicalReportByAppointmentHandler(
        IMedicalReportRepository medicalReportRepository,
        ICurrentUser currentUser)
    {
        _medicalReportRepository = medicalReportRepository;
        _currentUser = currentUser;
    }

    public async Task<MedicalReportDto?> Handle(
        GetMedicalReportByAppointmentQuery request,
        CancellationToken cancellationToken)
    {
        var report = await _medicalReportRepository.GetByAppointmentIdAsync(request.AppointmentId);
        if (report is null)
        {
            return null;
        }

        var isAssignedDoctor = report.DoctorId == _currentUser.DoctorId;
        var isOwnerPatient = report.PatientId == _currentUser.PatientId;
        var hasAnyScope = _currentUser.HasPermission("medical-report.view.any");

        if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
        {
            throw new ForbiddenException("You do not have permission to view this medical report.");
        }

        return new MedicalReportDto
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
        };
    }
}
