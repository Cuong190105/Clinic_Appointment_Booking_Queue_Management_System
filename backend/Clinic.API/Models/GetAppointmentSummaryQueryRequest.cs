namespace Clinic.API.Models
{
    public class GetAppointmentSummaryQueryRequest
    {
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        public Guid DoctorId { get; set; } = Guid.Empty;
        public Guid SpecialtyId { get; set; } = Guid.Empty;
    }
}