using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Commands
{
    public record CreateAmendmentCommand(
        Guid MedicalReportId,
        string Reason,
        string Content
    ) : IRequest<MedicalReportAmendmentDto>;

    public class CreateAmendmentHandler : IRequestHandler<CreateAmendmentCommand, MedicalReportAmendmentDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public CreateAmendmentHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser,
            IUnitOfWork unitOfWork)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }

        public async Task<MedicalReportAmendmentDto> Handle(CreateAmendmentCommand request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.MedicalReportId)
                ?? throw new NotFoundException($"Medical report '{request.MedicalReportId}' not found.");

            var doctorId = report.Appointment?.WorkSchedule?.DoctorId ?? report.QueueTicket?.Appointment?.WorkSchedule?.DoctorId;
            var isAssignedDoctor = doctorId.HasValue && doctorId == _currentUser.DoctorId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any") || _currentUser.IsInRole("Admin");

            if (!isAssignedDoctor && !hasAnyScope)
            {
                throw new ForbiddenException("You are not allowed to add amendments to another doctor's medical report.");
            }

            if (report.Status != MedicalReportStatus.Finalized)
            {
                throw new InvalidOperationException("Amendments can only be added to finalized medical reports.");
            }

            var authorDoctorId = _currentUser.DoctorId ?? doctorId ?? Guid.Empty;
            var doctorName = report.Appointment?.WorkSchedule?.Doctor?.Employee?.Person?.FullName ?? "Bác sĩ";

            var amendment = report.AddAmendment(request.Reason, request.Content, authorDoctorId, doctorName);
            await _medicalReportRepository.AddAmendmentAsync(amendment);

            var audit = new MedicalAccessAudit(
                report.Id,
                _currentUser.UserId ?? Guid.Empty,
                "AddAmendment",
                $"Added amendment with reason: {request.Reason}"
            );
            await _medicalReportRepository.AddAuditAsync(audit);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MedicalReportAmendmentDto
            {
                Id = amendment.Id,
                MedicalReportId = amendment.MedicalReportId,
                Reason = amendment.Reason,
                Content = amendment.Content,
                CreatedByDoctorId = amendment.CreatedByDoctorId,
                DoctorName = amendment.DoctorName,
                CreatedAt = amendment.CreatedAt
            };
        }
    }
}
