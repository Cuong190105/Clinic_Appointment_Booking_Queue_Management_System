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
                    <h2 style="color: #333;">Đặt lại mật khẩu</h2>

                    <p>Xin chào {info.FullName},</p>

                    <p>Chúng tôi vừa nhận được yêu cầu đặt lại mật khẩu của bạn.</p>

                    <p>
                        Mã đặt lại mật khẩu của bạn là: <strong>{info.PasswordResetCode}</strong>
                    </p>
                    <p>Mã này chỉ dùng một lần và hết hạn sau 30 phút. Nhập mã cùng mật khẩu mới trên màn hình Quên mật khẩu.</p>

                    <p>Trân trọng,<br>Phòng khám của chúng tôi</p>
                </body>
                </html>
                """;
            return new EmailContent("Đặt lại mật khẩu", body);
        }

        public SmsContent RenderSms(Account info)
        {
            var message = $"Mã đặt lại mật khẩu: {info.PasswordResetCode}. Mã chỉ dùng một lần và hết hạn sau 30 phút.";
            return new SmsContent(message);
        }
    }
}
