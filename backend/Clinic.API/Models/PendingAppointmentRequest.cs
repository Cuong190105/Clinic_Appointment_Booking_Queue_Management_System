namespace Clinic.API.Models
{
    public class PendingAppointmentRequest
    {
        public string Search { get; set; } = string.Empty;
        public string SortBy { get; set; } = string.Empty;
        public string OrderBy { get; set; } = string.Empty;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
