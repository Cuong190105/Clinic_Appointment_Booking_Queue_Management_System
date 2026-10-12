using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    public class GetSpecialtiesRequest
    {
        private const int MaxPageSize = 100;
        public string? Search { get; set; }
        public string SortBy { get; set; } = "name";
        public bool Descending { get; set; } = false;

        [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
        public int Page { get; set; } = 1;

        [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; set; } = 20;
    }
}

