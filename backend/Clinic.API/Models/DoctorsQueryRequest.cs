using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class DoctorsQueryRequest
    {
        [MaxLength(50, ErrorMessage = "Search term must be at most 50 characters.")]
        public string? Search { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Qualification field must be at most 50 characters.")]
        public string? Qualification { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "Sort field must be at most 50 characters.")]
        public string? SortBy { get; set; } = string.Empty;

        public DoctorStatus? Status { get; set; } = null;

        public Guid? SpecialtyId { get; set; } = null;

        [MaxLength(5, ErrorMessage = "Order field must be either 'asc' or 'desc'. Default is 'asc'.")]
        public string? OrderBy { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        public int PageSize { get; set; } = 10;
    }
}
