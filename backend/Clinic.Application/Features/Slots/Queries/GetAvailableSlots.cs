using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Slots.Queries
{
    public record GetAvailableSlotsQuery(
        Guid? DoctorId = null,
        Guid? SpecialtyId = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    ) : IRequest<List<SlotDto>>;

    public class GetAvailableSlotsQueryHandler : IRequestHandler<GetAvailableSlotsQuery, List<SlotDto>>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;

        public GetAvailableSlotsQueryHandler(IWorkScheduleRepository workScheduleRepository)
        {
            _workScheduleRepository = workScheduleRepository;
        }

        public async Task<List<SlotDto>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
        {
            var slots = await _workScheduleRepository.GetAvailableSlotsAsync(
                request.DoctorId, request.SpecialtyId, request.FromDate, request.ToDate);

            return slots.Select(w => new SlotDto
            {
                WorkScheduleId = w.Id,
                DoctorId = w.DoctorId,
                DoctorName = w.Doctor?.Employee?.Person?.FullName ?? string.Empty,
                Date = w.Date,
                ShiftStart = w.ShiftStart,
                ShiftEnd = w.ShiftEnd,
                RemainingCapacity = w.PatientLimit - w.Appointments.Count(a => a.Status != AppointmentStatus.Cancelled)
            }).ToList();
        }
    }
}