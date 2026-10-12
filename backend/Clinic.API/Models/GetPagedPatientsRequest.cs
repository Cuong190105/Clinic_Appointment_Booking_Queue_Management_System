using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class GetPagedPatientsRequest
    {
        [MaxLength(100)]
        public string? Search { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? SortBy { get; set; } = "fullName";
        [MaxLength(5, ErrorMessage = "OrderBy must be either 'asc' or 'desc'")]
        public string? OrderBy { get; set; } = "asc";
        [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than 0")]
        public int PageNumber { get; set; } = 1;
        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
        public int PageSize { get; set; } = 10;
    }
}
