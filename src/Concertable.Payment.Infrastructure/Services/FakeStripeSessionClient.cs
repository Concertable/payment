using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Infrastructure.Services;

internal enum FakeStripeSessionFaultPoint
{
    BeforeProviderAcceptance,
    AfterProviderAcceptance,
    BeforeCustomerSessionResponse
}

internal sealed class FakeStripeSessionClient : IStripeSessionClient
{
    private readonly ConcurrentDictionary<string, PaymentSessionProviderResult> byIdempotencyKey = [];
    private readonly ConcurrentDictionary<string, PaymentSessionProviderResult> byProviderObjectId = [];
    private readonly ConcurrentDictionary<FakeStripeSessionFaultPoint, byte> oneShotFaults = [];
    private readonly TimeProvider timeProvider;

    public FakeStripeSessionClient(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    internal int ProviderObjectCount => byProviderObjectId.Count;

    internal void FailOnce(FakeStripeSessionFaultPoint faultPoint) =>
        oneShotFaults[faultPoint] = 0;

    public Task<PaymentSessionProviderResult> CreateAsync(
        PaymentSessionProviderRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ThrowIfRequested(FakeStripeSessionFaultPoint.BeforeProviderAcceptance);

        var result = byIdempotencyKey.GetOrAdd(idempotencyKey, _ => Create(request, idempotencyKey));
        byProviderObjectId.TryAdd(result.ProviderObjectId, result);

        ThrowIfRequested(FakeStripeSessionFaultPoint.AfterProviderAcceptance);
        return Task.FromResult(result);
    }

    public Task<PaymentSessionProviderResult> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!byProviderObjectId.TryGetValue(providerObjectId, out var result)
            || result.ProviderObjectKind != providerObjectKind)
        {
            throw new PaymentSessionProviderUnavailableException("The fake Stripe session does not exist.");
        }

        return Task.FromResult(result);
    }

    public Task<PaymentSessionProviderResult> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!byProviderObjectId.TryGetValue(providerObjectId, out var current)
            || current.ProviderObjectKind != providerObjectKind
            || !current.CanCancel)
        {
            throw new PaymentSessionProviderUnavailableException("The fake Stripe session cannot be canceled.");
        }

        var canceled = current with
        {
            Status = "canceled",
            ObservedAt = timeProvider.GetUtcNow(),
            IsExplicitConsumerCancellation = true,
            CanCancel = false
        };
        byProviderObjectId[providerObjectId] = canceled;
        foreach (var entry in byIdempotencyKey.Where(entry => entry.Value.ProviderObjectId == providerObjectId))
            byIdempotencyKey[entry.Key] = canceled;

        return Task.FromResult(canceled);
    }

    public Task<string> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ThrowIfRequested(FakeStripeSessionFaultPoint.BeforeCustomerSessionResponse);
        return Task.FromResult($"cuss_fake_{providerCustomerId}_{Guid.CreateVersion7():N}_secret");
    }

    internal void SetStatus(
        string providerObjectId,
        string status,
        DateTimeOffset? captureBefore = null)
    {
        var current = byProviderObjectId[providerObjectId];
        var updated = current with
        {
            Status = status,
            ObservedAt = timeProvider.GetUtcNow(),
            CanCancel = status is not ("succeeded" or "canceled"),
            CaptureBefore = captureBefore
        };
        byProviderObjectId[providerObjectId] = updated;
        foreach (var entry in byIdempotencyKey.Where(entry => entry.Value.ProviderObjectId == providerObjectId))
            byIdempotencyKey[entry.Key] = updated;
    }

    private PaymentSessionProviderResult Create(PaymentSessionProviderRequest request, string idempotencyKey)
    {
        var isPayment = request.SessionKind is PaymentSessionKind.Payment or PaymentSessionKind.Authorization;
        var prefix = isPayment ? "pi_fake" : "seti_fake";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey))).ToLowerInvariant();
        var id = $"{prefix}_{hash[..24]}";
        return new(
            isPayment
                ? PaymentSessionProviderObjectKind.PaymentIntent
                : PaymentSessionProviderObjectKind.SetupIntent,
            id,
            "requires_confirmation",
            timeProvider.GetUtcNow(),
            null,
            null,
            false,
            true,
            $"{id}_secret_fake",
            $"req_fake_{hash[..16]}",
            null,
            null);
    }

    private void ThrowIfRequested(FakeStripeSessionFaultPoint faultPoint)
    {
        if (oneShotFaults.TryRemove(faultPoint, out _))
            throw new PaymentSessionProviderUnavailableException($"Injected fake Stripe fault at {faultPoint}.");
    }
}
