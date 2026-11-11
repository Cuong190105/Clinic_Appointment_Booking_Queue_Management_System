using System;

namespace Clinic.Domain.Common
{
    public class TimeConverter
    {
        public static readonly TimeZoneInfo VietnamTimeZone = ResolveTimeZone();

        private static TimeZoneInfo ResolveTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "ICT", "ICT");
                }
            }
        }

        public DateTime ConvertToUtc(DateTime localDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, VietnamTimeZone);
        }

        public DateTime ConvertFromUtc(DateTime utcDateTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, VietnamTimeZone);
        }

        public DateTime ConvertToLocal(DateTime utcDateTime) => ConvertFromUtc(utcDateTime);

        public DateTime LocalNow => ConvertFromUtc(DateTime.UtcNow);

        public DateOnly Today => DateOnly.FromDateTime(LocalNow);

        public TimeOnly CurrentTime => TimeOnly.FromDateTime(LocalNow);
    }
}
