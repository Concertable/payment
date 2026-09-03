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
    private readonly ConcurrentDictionary<
        PaymentSessionIdempotencyKey,
        ProviderSession> byIdempotencyKey = [];
    private readonly ConcurrentDictionary<string, ProviderSession> byProviderObjectId = [];
    private readonly ConcurrentDictionary<FakeStripeSessionFaultPoint, byte> oneShotFaults = [];
    private readonly TimeProvider timeProvider;

    public FakeStripeSessionClient(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    internal int ProviderObjectCount => byProviderObjectId.Count;

    internal void FailOnce(FakeStripeSessionFaultPoint faultPoint) =>
        oneShotFaults[faultPoint] = 0;

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CreateAsync(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (TakeFault(FakeStripeSessionFaultPoint.BeforeProviderAcceptance))
        {
            Result<ProviderSession, PaymentOperationError.ProviderUnavailable> unavailable =
                new PaymentOperationError.ProviderUnavailable();
            return Task.FromResult(unavailable);
        }

        var result = byIdempotencyKey.GetOrAdd(idempotencyKey, _ => Create(request, idempotencyKey));
        byProviderObjectId.TryAdd(result.ProviderObjectId, result);

        if (TakeFault(FakeStripeSessionFaultPoint.AfterProviderAcceptance))
        {
            Result<ProviderSession, PaymentOperationError.ProviderUnavailable> unavailable =
                new PaymentOperationError.ProviderUnavailable();
            return Task.FromResult(unavailable);
        }

        Result<ProviderSession, PaymentOperationError.ProviderUnavailable> success = result;
        return Task.FromResult(success);
    }

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> RetrieveAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!byProviderObjectId.TryGetValue(providerObjectId, out var result)
            || result.ProviderObjectKind != providerObjectKind)
        {
            Result<ProviderSession, PaymentOperationError.ProviderUnavailable> unavailable =
                new PaymentOperationError.ProviderUnavailable();
            return Task.FromResult(unavailable);
        }

        Result<ProviderSession, PaymentOperationError.ProviderUnavailable> success = result;
        return Task.FromResult(success);
    }

    public Task<Result<ProviderSession, PaymentOperationError.ProviderUnavailable>> CancelAsync(
        PaymentSessionProviderObjectKind providerObjectKind,
        string providerObjectId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!byProviderObjectId.TryGetValue(providerObjectId, out var current)
            || current.ProviderObjectKind != providerObjectKind
            || !current.CanCancel)
        {
            Result<ProviderSession, PaymentOperationError.ProviderUnavailable> unavailable =
                new PaymentOperationError.ProviderUnavailable();
            return Task.FromResult(unavailable);
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

        Result<ProviderSession, PaymentOperationError.ProviderUnavailable> success = canceled;
        return Task.FromResult(success);
    }

    public Task<Result<string, PaymentOperationError.ProviderUnavailable>> CreateCustomerSessionAsync(
        string providerCustomerId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (TakeFault(FakeStripeSessionFaultPoint.BeforeCustomerSessionResponse))
        {
            Result<string, PaymentOperationError.ProviderUnavailable> unavailable =
                new PaymentOperationError.ProviderUnavailable();
            return Task.FromResult(unavailable);
        }

        Result<string, PaymentOperationError.ProviderUnavailable> success =
            $"cuss_fake_{providerCustomerId}_{Guid.CreateVersion7():N}_secret";
        return Task.FromResult(success);
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
            CaptureBefore = captureBefore,
            PaymentMethodId = status == "succeeded"
                && current.ProviderObjectKind == PaymentSessionProviderObjectKind.SetupIntent
                    ? $"pm_fake_{providerObjectId}"
                    : current.PaymentMethodId
        };
        byProviderObjectId[providerObjectId] = updated;
        foreach (var entry in byIdempotencyKey.Where(entry => entry.Value.ProviderObjectId == providerObjectId))
            byIdempotencyKey[entry.Key] = updated;
    }

    internal void SetDeclined(string providerObjectId)
    {
        var current = byProviderObjectId[providerObjectId];
        var updated = current with
        {
            Status = "requires_payment_method",
            ObservedAt = timeProvider.GetUtcNow(),
            FailureClassification = ProviderFailureClassification.Declined,
            CanCancel = true,
            CaptureBefore = null
        };
        byProviderObjectId[providerObjectId] = updated;
        foreach (var entry in byIdempotencyKey.Where(entry => entry.Value.ProviderObjectId == providerObjectId))
            byIdempotencyKey[entry.Key] = updated;
    }

    private ProviderSession Create(
        PaymentSessionProviderRequest request,
        PaymentSessionIdempotencyKey idempotencyKey)
    {
        var isPayment = request.SessionKind is PaymentSessionKind.Payment or PaymentSessionKind.Authorization;
        var prefix = isPayment ? "pi_fake" : "seti_fake";
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey.ToString())))
            .ToLowerInvariant();
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
            null,
            $"req_fake_{hash[..16]}",
            null,
            null);
    }

    private bool TakeFault(FakeStripeSessionFaultPoint faultPoint) =>
        oneShotFaults.TryRemove(faultPoint, out _);
}
