using System;
using System.Net;
using Clinic.API.Models;
using Clinic.Application.Common.Exceptions;
using Clinic.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Clinic.API.Common
{
    /// <summary>
    /// Bắt MỌI exception chưa được xử lý và trả về đúng envelope ApiResponse.
    /// Nhờ đó client luôn nhận cùng một hình dạng kể cả khi có lỗi.
    /// </summary>
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (statusCode, messages) = exception switch
            {
                NotFoundException => (HttpStatusCode.NotFound, new[] { exception.Message }),
                ForbiddenException => (HttpStatusCode.Forbidden, new[] { exception.Message }),
                // Vi phạm business rule hoặc xung đột phiên bản
                ConcurrencyConflictException => (HttpStatusCode.Conflict, new[] { exception.Message }),
                DbUpdateConcurrencyException => (HttpStatusCode.Conflict, new[] { "Dữ liệu đã bị thay đổi bởi thao tác khác, vui lòng thử lại." }),
                ConflictException => (HttpStatusCode.Conflict, new[] { exception.Message }),
                // Domain ném ArgumentException khi dữ liệu đầu vào không hợp lệ.
                ArgumentException => (HttpStatusCode.BadRequest, new[] { exception.Message }),
                InvalidOperationException => (HttpStatusCode.BadRequest, new[] { exception.Message }),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, new[] { exception.Message }),
                _ => (HttpStatusCode.InternalServerError, new[] { exception.InnerException?.Message ?? exception.Message ?? "An unexpected error occurred." })
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                // Chỉ log chi tiết cho lỗi ngoài dự kiến; không rò rỉ thông tin ra client.
                _logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
            }

            var response = ApiResponse.Fail(statusCode, messages);
            httpContext.Response.StatusCode = (int)statusCode;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true;
        }
    }
}
