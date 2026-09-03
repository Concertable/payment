using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Services;
using Reunion;

namespace Concertable.Payment.IntegrationTests.Fixtures;

internal sealed class ControllableStripeSessionClient : IStripeSessionClient
{
    private readonly FakeStripeSessionClient inner;
    private volatile bool retrievalUnavailable;

    public ControllableStripeSessionClient(FakeStripeSessionClient inner)
    {
        this.inner = inner;
    }

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default) =>
        inner.CreateAsync(request, idempotencyKey, ct);

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        retrievalUnavailable
            ? Task.FromResult<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>>(
                new PaymentOperationError.ProviderUnavailable())
            : inner.RetrieveAsync(providerObjectKind, providerObjectId, ct);

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default) =>
        inner.CancelAsync(providerObjectKind, providerObjectId, ct);

    public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default) =>
        inner.CreateCustomerSessionAsync(providerCustomerId, ct);

    internal void Reset() => retrievalUnavailable = false;

    internal void SetRetrievalUnavailable(bool unavailable) =>
        retrievalUnavailable = unavailable;

    internal void SetStatus(
        string providerObjectId,
        string status,
        DateTimeOffset? captureBefore) =>
        inner.SetStatus(providerObjectId, status, captureBefore);
}
