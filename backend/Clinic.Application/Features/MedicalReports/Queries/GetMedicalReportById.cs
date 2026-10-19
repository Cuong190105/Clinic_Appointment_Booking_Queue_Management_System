using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Clinic.Application.Features.MedicalReports.Queries
{
    public record GetMedicalReportByIdQuery(Guid Id) : IRequest<MedicalReportDto>;

    public class GetMedicalReportByIdHandler : IRequestHandler<GetMedicalReportByIdQuery, MedicalReportDto>
    {
        private readonly IMedicalReportRepository _medicalReportRepository;
        private readonly ICurrentUser _currentUser;

        public GetMedicalReportByIdHandler(
            IMedicalReportRepository medicalReportRepository,
            ICurrentUser currentUser)
        {
            _medicalReportRepository = medicalReportRepository;
            _currentUser = currentUser;
        }

        public async Task<MedicalReportDto> Handle(GetMedicalReportByIdQuery request, CancellationToken cancellationToken)
        {
            var report = await _medicalReportRepository.GetByIdWithOwnershipAsync(request.Id)
                ?? throw new NotFoundException($"Medical report '{request.Id}' not found.");

            var appointment = report.Appointment;
            var isAssignedDoctor = appointment.WorkSchedule.DoctorId == _currentUser.DoctorId;
            var isOwnerPatient = appointment.PatientId == _currentUser.PatientId;
            var hasAnyScope = _currentUser.HasPermission("medical-report.view.any");

            if (!isAssignedDoctor && !isOwnerPatient && !hasAnyScope)
            {
                throw new ForbiddenException("You do not have permission to view this medical report.");
            }

            return new MedicalReportDto
            {
                Id = report.Id,
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                PatientName = appointment.Patient?.Person?.FullName ?? string.Empty,
                DoctorId = appointment.WorkSchedule.DoctorId,
                DoctorName = appointment.WorkSchedule.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                ExamDate = appointment.WorkSchedule.Date,
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
}
