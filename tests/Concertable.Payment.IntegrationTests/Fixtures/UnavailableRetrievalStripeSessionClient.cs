using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Enums;
using Reunion;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class UnavailableRetrievalStripeSessionClient : IStripeSessionClient
{
    private readonly IStripeSessionClient inner;

    public UnavailableRetrievalStripeSessionClient(IStripeSessionClient inner)
    {
        this.inner = inner;
    }

    public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default) =>
        inner.CreateAsync(request, idempotencyKey, ct);

    public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable> unavailable =
            new PaymentOperationError.ProviderUnavailable();
        return Task.FromResult(unavailable);
    }

    public Task<Result<PaymentSessionProviderResult, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        inner.CancelAsync(providerObjectKind, providerObjectId, ct);

    public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default) =>
        inner.CreateCustomerSessionAsync(providerCustomerId, ct);
}
