using System.ComponentModel.DataAnnotations;

namespace Clinic.API.Models
{
    /// <summary>
    /// Tham sá»‘ phÃ¢n trang Client gá»­i lÃªn (thÆ°á»ng qua query string: ?pageNumber=1&pageSize=10).
    /// </summary>
    public class PaginationRequest
    {
        private const int MaxPageSize = 100;

        [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be at least 1.")]
        public int PageNumber { get; set; } = 1;

        // Cháº·n trÃªn Ä‘á»ƒ trÃ¡nh Client yÃªu cáº§u trang quÃ¡ lá»›n: vÆ°á»£t ngÆ°á»¡ng sáº½ tráº£ vá» 400 (validation),
        // thay vÃ¬ Ã¢m tháº§m cáº¯t giÃ¡ trá»‹ (khiáº¿n [Range] vÃ´ nghÄ©a vÃ  Client khÃ´ng biáº¿t yÃªu cáº§u bá»‹ Ä‘á»•i).
        [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; set; } = 10;
    }
}
