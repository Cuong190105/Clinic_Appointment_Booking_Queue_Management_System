using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class PagedUsersQueryRequest
    {
        [MaxLength(50, ErrorMessage = "Search term must be at most 50 characters.")]
        public string? Search { get; set; } = string.Empty;
        public string? Role { get; set; }
        [MaxLength(50, ErrorMessage = "Sort field must be at most 50 characters.")]
        public string? SortBy { get; set; } = string.Empty;
        [MaxLength(5, ErrorMessage = "Order field must be 'asc' or 'desc'. Default is 'asc'.")]
        public string? OrderBy { get; set; } = string.Empty;
        public Gender? Gender { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0.")]
        public int? PageNumber { get; set; } = 1;
        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        public int? PageSize { get; set; } = 10;
    }
}
