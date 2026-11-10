using System;

namespace Clinic.Application.Common.Exceptions
{
    /// <summary>
    /// NÃ©m ra khi khÃ´ng tÃ¬m tháº¥y tÃ i nguyÃªn yÃªu cáº§u. ÄÆ°á»£c GlobalExceptionHandler Ã¡nh xáº¡ sang HTTP 404.
    /// </summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message)
        {
        }
    }
}
