using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Clinic.Application.Contracts
{
    /// <summary>
    /// Káº¿t quáº£ phÃ¢n trang tráº£ vá» Client: danh sÃ¡ch báº£n ghi cá»§a trang hiá»‡n táº¡i kÃ¨m metadata.
    /// ÄÆ°á»£c cÃ¡c Query handler dá»±ng sáºµn Ä‘á»ƒ táº§ng API chá»‰ viá»‡c tráº£ vá».
    /// </summary>
    public class PaginationResponse<T>
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<T> Items { get; set; } = new List<T>();

        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        [JsonPropertyName("hasPrevious")]
        public bool HasPrevious => PageNumber > 1;

        [JsonPropertyName("hasNext")]
        public bool HasNext => PageNumber < TotalPages;

        public PaginationResponse() { }

        public PaginationResponse(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            PageNumber = pageNumber;
            PageSize = pageSize;
        }
    }
}
