namespace Clinic.Notification.Service.Services.Delivery;

public enum ProviderResultCategory
{
    Success,
    TransientFailure,
    PermanentFailure,
    Timeout,
    Ambiguous
}

public sealed record ProviderDeliveryResult(
    ProviderResultCategory Category,
    string ProviderResponse,
    string? ProviderReference = null
);

