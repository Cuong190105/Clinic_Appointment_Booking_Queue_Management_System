namespace Clinic.Application.Interfaces
{
    public interface ISmsSender
    {
        Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    }
}
