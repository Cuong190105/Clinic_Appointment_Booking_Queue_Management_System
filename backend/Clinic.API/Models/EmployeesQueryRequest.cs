using Clinic.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class EmployeesQueryRequest
    {
        [MaxLength(50, ErrorMessage = "Search term must be at most 50 characters.")]
        public string? Search { get; set; } = string.Empty;
        public EmployeeStatus? Status { get; set; } = null;

        [MaxLength(50, ErrorMessage = "Sort field must be at most 50 characters.")]
        public string? SortBy { get; set; } = string.Empty;

        [MaxLength(5, ErrorMessage = "Order field must be either 'asc' or 'desc'. Default is 'asc'.")]
        public string? OrderBy { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int PageNumber { get; set; } = 1;

        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        public int PageSize { get; set; } = 10;
    }
}
