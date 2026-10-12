using Clinic.Domain.Enums;

namespace Clinic.Application.Contracts
{
    public class WorkScheduleDto
    {
        public Guid Id { get; set; }
        public Guid DoctorId { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int PatientLimit { get; set; }
        public WorkScheduleStatus Status { get; set; }
    }
}
