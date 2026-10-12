namespace Clinic.API.Models
{
    public class AdminGetShiftsQuery : GetShiftsQuery
    {
        public Guid SpecialtyId { get; set; } = Guid.Empty;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SearchTerm { get; set; } = string.Empty;
    }
}
