using Concertable.Payment.Application.PaymentSessions;

namespace Concertable.Payment.Application.Interfaces;

internal interface IStripeSessionClient
{
    Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default);

    Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default);

    Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default);
}
