using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Appointments.Commands
{
    // Use-case: Patient or Receptionist cancels an appointment
    public record CancelAppointmentCommand(
        Guid AppointmentId
    ) : IRequest<Guid>;
    public class CancelAppointmentCommandHandler : IRequestHandler<CancelAppointmentCommand, Guid>
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly ICurrentUser _currentUser;
        private readonly IUnitOfWork _unitOfWork;

        public CancelAppointmentCommandHandler(IAppointmentRepository appointmentRepository, ICurrentUser currentUser, IUnitOfWork unitOfWork)
        {
            _appointmentRepository = appointmentRepository;
            _currentUser = currentUser;
            _unitOfWork = unitOfWork;
        }
        public async Task<Guid> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId == null)
            {
                throw new UnauthorizedAccessException();
            }
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId);
            if (appointment == null)
            {
                throw new NotFoundException("Appointment not found.");
            }
            if (!(_currentUser.HasPermission("appointment.edit.any") || _currentUser.PatientId == appointment.PatientId))
            {
                throw new ForbiddenException("You are not authorized to cancel this appointment.");
            }
            appointment.Cancel((Guid)_currentUser.UserId);
            await _unitOfWork.SaveChangesAsync();
            return request.AppointmentId;
        }
    }
}
