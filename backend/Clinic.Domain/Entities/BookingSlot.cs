using Clinic.Domain.Common;
using Clinic.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Domain.Entities
{
    public class BookingSlot
    {
        public Guid Id { get; protected set; }
        public Guid WorkScheduleId { get; protected set; }
        public TimeOnly TimeSlot { get; protected set; }
        public BookingSlotStatus Status { get; protected set; }

        // Navigation properties
        public WorkSchedule WorkSchedule { get; protected set; }

        // Navigation properties
        public Appointment? Appointment { get; protected set; }

        private BookingSlot() { } // For EF Core

        public BookingSlot(BookingSlot bookingSlot)
        {
            Id = bookingSlot.Id;
            WorkScheduleId = bookingSlot.WorkScheduleId;
            TimeSlot = bookingSlot.TimeSlot;
            Status = bookingSlot.Status;
            WorkSchedule = bookingSlot.WorkSchedule;
        }
    }
}
