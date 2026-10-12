using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Common;
using Clinic.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.WorkSchedules.Queries
{
    // Use-case: Get available work schedules for a doctor on a specific date for booking
    public record GetDoctorSchedulesForBookingQuery(
        Guid DoctorId,
        DateOnly Date
    ) : IRequest<List<WorkSchedulesBookingDto>>;
    public class GetDoctorSchedulesForBookingQueryHandler : IRequestHandler<GetDoctorSchedulesForBookingQuery, List<WorkSchedulesBookingDto>>
    {
        private readonly IWorkScheduleRepository _workScheduleRepository;
        private readonly IDoctorRepository _doctorRepository;

        public GetDoctorSchedulesForBookingQueryHandler(IWorkScheduleRepository workScheduleRepository, IDoctorRepository doctorRepository)
        {
            _workScheduleRepository = workScheduleRepository;
            _doctorRepository = doctorRepository;
        }
        
        public async Task<List<WorkSchedulesBookingDto>> Handle(GetDoctorSchedulesForBookingQuery request, CancellationToken cancellationToken)
        {
            if (request.Date < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ArgumentException("Cannot get schedules for a past date.");
            }
            if (await _doctorRepository.GetInfoByIdAsync(request.DoctorId) == null)
            {
                throw new ArgumentException("Doctor not found.");
            }
            var workSchedules = await _workScheduleRepository.GetDoctorSchedulesWithAppointmentByDateAsync(request.DoctorId, request.Date);
            var appointmentSlots = new List<WorkSchedulesBookingDto>();
            foreach (var workSchedule in workSchedules)
            {
                var workScheduleDto = new WorkSchedulesBookingDto
                {
                    Id = workSchedule.Id,
                    StartTime = workSchedule.ShiftStart,
                    EndTime = workSchedule.ShiftEnd,
                };
                var slotTime = workSchedule.ShiftStart;
                var slotTimeUtc = new TimeConverter().ConvertToUtc(new DateTime(workSchedule.Date, slotTime));
                while (slotTime < workSchedule.ShiftEnd)
                {
                    // Check if the slot is available for booking (not in the past and not already booked)
                    if (slotTimeUtc >= DateTime.UtcNow.AddHours(2) && !workSchedule.Appointments.Any(a => a.TimeSlot == slotTime))
                    {
                        workScheduleDto.TimeSlot.Add(slotTime);
                    }
                    slotTime = slotTime.AddMinutes(WorkSchedule.SlotIntervalMinutes);
                    slotTimeUtc = slotTimeUtc.AddMinutes(WorkSchedule.SlotIntervalMinutes);
                }
                appointmentSlots.Add(workScheduleDto);
            }
            return appointmentSlots;
        }
    }
}
