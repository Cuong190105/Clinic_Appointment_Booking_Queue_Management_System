using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Queries
{
    public record GetMedicalReportsQuery(
        string? Search,
        int? Status,
        DateTime? FromDate,
        DateTime? ToDate,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<MedicalReportDto>>;

    public class GetMedicalReportsHandler : IRequestHandler<GetMedicalReportsQuery, PaginationResponse<MedicalReportDto>>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;

        public GetMedicalReportsHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
        }

        public async Task<PaginationResponse<MedicalReportDto>> Handle(GetMedicalReportsQuery request, CancellationToken cancellationToken)
        {
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any") || _currentUser.IsInRole("Admin");
            var isDoctorScoped = !hasAnyScope && _currentUser.DoctorId.HasValue;
            var doctorId = _currentUser.DoctorId;

            return await _medicalReportRepository.GetPagedReportsAsync(
                doctorId,
                isDoctorScoped,
                request.Search,
                request.Status,
                request.FromDate,
                request.ToDate,
                request.PageNumber,
                request.PageSize,
                cancellationToken
            );
        }
    }
}
