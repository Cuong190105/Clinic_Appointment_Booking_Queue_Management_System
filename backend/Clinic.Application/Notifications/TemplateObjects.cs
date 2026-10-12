namespace Clinic.Application.Notifications
{
    public record EmailContent(string Subject, string Body);
    public record InAppContent(string Title, string Message);
    public record SmsContent(string Message);
}
