using System;

namespace Clinic.Application.Common.Exceptions
{
    /// <summary>
    /// Ném ra khi 1 b?n ghi dã b? request khác thay d?i tru?c, gi?a lúc d?c và lúc ghi
    /// c?a request hi?n t?i (optimistic concurrency conflict qua RowVersion).
    /// Khác ConflictException: dây không ph?i business rule, mà là tín hi?u "hãy d?c l?i
    /// và th? l?i", nên thu?ng du?c x? lý b?ng cách retry ngay trong Handler thay vì
    /// tr? th?ng l?i v? client.
    /// </summary>
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(string message) : base(message)
        {
        }
    }
}