using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Templates
{
    public class CustomNotificationTemplate
    {
        public InAppContent RenderInApp(CustomNotificationData data)
        {
            return new InAppContent(data.Title, data.Message);
        }

        public EmailContent RenderEmail(CustomNotificationData data)
        {
            var body = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nhận lịch khám</h2>

                    <p>Xin chào {data.FullName},</p>

                    <p>Phòng khám của chúng tôi xin thông báo:</p>

                    <p>{data.Message}</p>

                    <p>Trân trọng,<br>Phòng khám của chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent(data.Title, body);
        }

        public SmsContent RenderSms(CustomNotificationData data)
        {
            return new SmsContent(data.Message);
        }
    }
}
