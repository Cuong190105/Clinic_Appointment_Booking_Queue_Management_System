using Clinic.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Notifications.Templates
{
    public class ResetPasswordTemplate
    {
        public EmailContent RenderEmail(Account info)
        {
            var body =
                $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2 style="color: #333;">Xác nh?n l?ch khám</h2>

                    <p>Xin chào {info.FullName},</p>

                    <p>Chúng tôi v?a nh?n du?c yêu c?u d?t l?i m?t kh?u c?a b?n.</p>

                    <p>
                        M?t kh?u m?i c?a b?n là: <strong>{info.Password}</strong>
                    </p>

                    <p>Vui lòng d?n dúng gi? d? d?m b?o l?ch trình khám b?nh c?a b?n.</p>

                    <p>N?u quý khách mu?n thay d?i ho?c h?y l?ch h?n, vui lòng th?c hi?n tru?c 24 gi? so v?i th?i gian dã h?n.</p>

                    <p>Trân tr?ng,<br>Phòng khám c?a chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Ð?t l?i m?t kh?u", body);
        }

        public SmsContent RenderSms(Account info)
        {
            var message = $"M?t kh?u m?i c?a b?n là: {info.Password}";
            return new SmsContent(message);
        }
    }
}
