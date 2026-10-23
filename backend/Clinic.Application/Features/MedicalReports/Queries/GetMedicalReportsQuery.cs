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
    public record GetMedicalReportsQuery(
        string? Search = null,
        MedicalReportStatus? Status = null,
        DateOnly? FromDate = null,
        DateOnly? ToDate = null,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<MedicalReportDto>>;

    public class GetMedicalReportsQueryHandler : IRequestHandler<GetMedicalReportsQuery, PaginationResponse<MedicalReportDto>>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;

        public GetMedicalReportsQueryHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
        }

        public async Task<PaginationResponse<MedicalReportDto>> Handle(GetMedicalReportsQuery request, CancellationToken cancellationToken)
        {
            var canViewAny = _currentUser.HasPermission("medical-report.view.any");
            Guid? doctorId = canViewAny ? null : _currentUser.DoctorId;

            var (items, totalCount) = await _medicalReportRepository.SearchAsync(
                doctorId,
                request.Search,
                request.Status,
                request.FromDate,
                request.ToDate,
                request.PageNumber,
                request.PageSize,
                cancellationToken);

            var dtos = items.Select(report =>
            {
                var appointment = report.Appointment;
                return new MedicalReportDto
                {
                    Id = report.Id,
                    AppointmentId = report.AppointmentId,
                    PatientId = report.PatientId,
                    PatientName = report.Patient?.Person?.FullName ?? appointment?.Patient?.Person?.FullName ?? string.Empty,
                    DoctorId = appointment?.WorkSchedule?.DoctorId ?? appointment?.DoctorId ?? Guid.Empty,
                    DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? appointment?.DoctorName ?? string.Empty,
                    SpecialtyName = appointment?.SpecialtyName ?? appointment?.WorkSchedule?.Doctor?.WorkHistories?.FirstOrDefault(wh => !wh.IsDeleted && wh.EndDate == null)?.Specialty?.Name ?? string.Empty,
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
            }).ToList();

            return new PaginationResponse<MedicalReportDto>(dtos, totalCount, request.PageNumber, request.PageSize);
        }
    }
}

