using Concertable.Payment.Application.PaymentSessions;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeSessionClient
{
    Task<PaymentSessionProviderResult> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default);

    Task<PaymentSessionProviderResult> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task<PaymentSessionProviderResult> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task<string> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default);
}
