using System.Net;
using Clinic.API.Models;
using Microsoft.AspNetCore.Diagnostics;

namespace Clinic.API.Common;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

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
            // Vi phạm business rule (vd: đổi lịch quá sát giờ hẹn) - request hợp lệ nhưng không thể xử lý ở trạng thái hiện tại.
            ConflictException => (HttpStatusCode.Conflict, new[] { exception.Message }),
            // Domain ném ArgumentException khi dữ liệu đầu vào không hợp lệ.
            ArgumentException => (HttpStatusCode.BadRequest, new[] { exception.Message }),
            InvalidOperationException => (HttpStatusCode.BadRequest, new[] { exception.Message }),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, new[] { exception.Message }),
            TimeoutException or TaskCanceledException when !httpContext.RequestAborted.IsCancellationRequested
                => (HttpStatusCode.GatewayTimeout, new[] { "Downstream microservice did not respond within the SLA threshold." }),
            HttpRequestException => (HttpStatusCode.BadGateway, new[] { "Downstream microservice connection failed or refused." }),
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
