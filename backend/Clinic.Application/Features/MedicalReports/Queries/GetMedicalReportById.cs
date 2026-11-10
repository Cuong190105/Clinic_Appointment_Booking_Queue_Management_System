using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Queries
{
    public record GetMedicalReportByIdQuery(Guid Id) : IRequest<MedicalReportDto>;

    public class GetMedicalReportByIdHandler : IRequestHandler<GetMedicalReportByIdQuery, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork? _unitOfWork;

        public GetMedicalReportByIdHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork? unitOfWork = null)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportDto> Handle(GetMedicalReportByIdQuery request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.Id)
                ?? throw new NotFoundException($"Medical report '{request.Id}' not found.");

            var appointment = report.Appointment ?? report.QueueTicket?.Appointment;
            var doctorId = appointment?.WorkSchedule?.DoctorId;
            var isAssignedDoctor = doctorId.HasValue && doctorId == _currentUser.DoctorId;
            var isOwnerPatient = appointment?.PatientId == _currentUser.PatientId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any") || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
            {
                throw new ForbiddenException("You do not have permission to view this medical report.");
            }

            var audit = new MedicalAccessAudit(
                report.Id,
                _currentUser.UserId ?? Guid.Empty,
                "View",
                $"Viewed medical report {report.Id}"
            );
            await _medicalReportRepository.AddAuditAsync(audit);
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
                AppointmentId = appointment?.Id ?? report.AppointmentId,
                QueueTicketId = report.QueueTicketId ?? appointment?.QueueTicket?.Id,
                PatientId = appointment?.PatientId ?? Guid.Empty,
                PatientName = appointment?.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = doctorId ?? Guid.Empty,
                DoctorName = appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                SpecialtyName = string.Empty,
                ExamDate = appointment?.WorkSchedule?.Date ?? DateOnly.MinValue,
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
