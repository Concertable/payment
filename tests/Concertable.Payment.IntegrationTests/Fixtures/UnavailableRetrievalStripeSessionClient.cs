using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Enums;
using Reunion;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class UnavailableRetrievalStripeSessionClient : IStripeSessionClient
{
    private readonly IStripeSessionClient stripeSessionClient;

    public UnavailableRetrievalStripeSessionClient(IStripeSessionClient stripeSessionClient)
    {
        this.stripeSessionClient = stripeSessionClient;
    }

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default) =>
        stripeSessionClient.CreateAsync(request, idempotencyKey, ct);

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        Result<ProviderSession, PaymentOperationError.ProviderUnavailable> unavailable =
            new PaymentOperationError.ProviderUnavailable();
        return Task.FromResult(unavailable);
    }

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        stripeSessionClient.CancelAsync(providerObjectKind, providerObjectId, ct);

    public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default) =>
        stripeSessionClient.CreateCustomerSessionAsync(providerCustomerId, ct);
}
