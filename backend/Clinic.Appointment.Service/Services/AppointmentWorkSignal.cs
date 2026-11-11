namespace Clinic.Appointment.Service.Services;

public interface IAppointmentWorkSignal
{
    void SignalConfirmation();
    Task WaitForConfirmationAsync(TimeSpan timeout, CancellationToken ct);
    void SignalOutbox();
    Task WaitForOutboxAsync(TimeSpan timeout, CancellationToken ct);
}

public sealed class AppointmentWorkSignal : IAppointmentWorkSignal
{
    private readonly SemaphoreSlim _confirmationSignal = new(0, 1);
    private readonly SemaphoreSlim _outboxSignal = new(0, 1);

    public void SignalConfirmation()
    {
        try
        {
            if (_confirmationSignal.CurrentCount == 0)
            {
                _confirmationSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public Task WaitForConfirmationAsync(TimeSpan timeout, CancellationToken ct)
        => _confirmationSignal.WaitAsync(timeout, ct);

    public void SignalOutbox()
    {
        try
        {
            if (_outboxSignal.CurrentCount == 0)
            {
                _outboxSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public Task WaitForOutboxAsync(TimeSpan timeout, CancellationToken ct)
        => _outboxSignal.WaitAsync(timeout, ct);
}
