using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace Clinic.API.Models
{
    /// <summary>
    /// Envelope chuáº©n cho Má»ŒI response cá»§a API.
    /// Client luÃ´n nháº­n Ä‘Æ°á»£c cÃ¹ng má»™t hÃ¬nh dáº¡ng: tráº¡ng thÃ¡i, cá» thÃ nh cÃ´ng, danh sÃ¡ch lá»—i vÃ  dá»¯ liá»‡u.
    /// </summary>
    public class ApiResponse
    {
        [JsonPropertyName("statusCode")]
        public HttpStatusCode StatusCode { get; set; }

        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; } = true;

        [JsonPropertyName("errorMessages")]
        public List<string> ErrorMessages { get; set; } = new();

        [JsonPropertyName("result")]
        public object? Result { get; set; }

        // ---- Factory helpers cho gá»n á»Ÿ controller ----

        public static ApiResponse Success(object? result, HttpStatusCode statusCode = HttpStatusCode.OK) => new()
        {
            StatusCode = statusCode,
            IsSuccess = true,
            Result = result
        };

        public static ApiResponse Fail(HttpStatusCode statusCode, params string[] errorMessages) => new()
        {
            StatusCode = statusCode,
            IsSuccess = false,
            ErrorMessages = new List<string>(errorMessages),
            Result = null
        };
    }
}
