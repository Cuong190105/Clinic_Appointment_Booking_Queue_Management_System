using System;

namespace Clinic.Application.Common.Exceptions
{
    /// <summary>
    /// Ném ra khi 1 bản ghi đã bị request khác thay đổi trước, giữa lúc đọc và lúc ghi
    /// của request hiện tại (optimistic concurrency conflict qua RowVersion).
    /// Khác ConflictException: đây không phải business rule, mà là tín hiệu "hãy đọc lại
    /// và thử lại", nên thường được xử lý bằng cách retry ngay trong Handler thay vì
    /// trả thẳng lỗi về client.
    /// </summary>
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(string message) : base(message)
        {
        }
    }
}