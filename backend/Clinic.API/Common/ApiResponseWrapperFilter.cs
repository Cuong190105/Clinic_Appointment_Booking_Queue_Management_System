using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Clinic.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Clinic.API.Common
{
    /// <summary>
    /// Tá»± Ä‘á»™ng bá»c Má»ŒI káº¿t quáº£ tráº£ vá» cá»§a controller vÃ o envelope ApiResponse.
    /// Nhá» Ä‘Ã³ controller chá»‰ cáº§n tráº£ vá» dá»¯ liá»‡u thÃ´ (Ok(data), Created(...), v.v.),
    /// khÃ´ng pháº£i tá»± dá»±ng ApiResponse á»Ÿ tá»«ng action.
    /// </summary>
    public class ApiResponseWrapperFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            switch (context.Result)
            {
                // ÄÃ£ lÃ  ApiResponse thÃ¬ giá»¯ nguyÃªn (vÃ­ dá»¥ output cá»§a GlobalExceptionHandler).
                case ObjectResult { Value: ApiResponse }:
                    break;

                // CÃ³ body: bá»c láº¡i Value, giá»¯ nguyÃªn loáº¡i result (Ä‘á»ƒ khÃ´ng máº¥t header Location cá»§a Created...).
                case ObjectResult objectResult:
                    objectResult.Value = Build(objectResult.Value, objectResult.StatusCode ?? StatusCodes.Status200OK);
                    break;

                // KhÃ´ng cÃ³ body (NoContent, StatusCode(...), NotFound() khÃ´ng kÃ¨m dá»¯ liá»‡u).
                case StatusCodeResult statusCodeResult:
                    context.Result = new ObjectResult(Build(null, statusCodeResult.StatusCode))
                    {
                        StatusCode = statusCodeResult.StatusCode
                    };
                    break;

                case EmptyResult:
                    context.Result = new ObjectResult(Build(null, StatusCodes.Status200OK))
                    {
                        StatusCode = StatusCodes.Status200OK
                    };
                    break;
            }

            await next();
        }

        private static ApiResponse Build(object? value, int statusCode)
        {
            if (statusCode is >= 200 and < 300)
            {
                return ApiResponse.Success(value, (HttpStatusCode)statusCode);
            }

            return ApiResponse.Fail((HttpStatusCode)statusCode, ExtractMessages(value, statusCode));
        }

        private static string[] ExtractMessages(object? value, int statusCode)
        {
            return value switch
            {
                // Lá»—i validation tá»± Ä‘á»™ng cá»§a [ApiController] (ModelState).
                ValidationProblemDetails vpd => vpd.Errors.SelectMany(kvp => kvp.Value).ToArray(),
                ProblemDetails pd => new[] { pd.Detail ?? pd.Title ?? DefaultMessage(statusCode) },
                string s => new[] { s },
                null => new[] { DefaultMessage(statusCode) },
                _ => new[] { value.ToString()! }
            };
        }

        private static string DefaultMessage(int statusCode) =>
            ((HttpStatusCode)statusCode).ToString();
    }
}
