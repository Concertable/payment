using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Infrastructure.Services;

namespace Concertable.Payment.UnitTests;

public sealed class PaymentSessionProviderExecutionTests
{
    [Fact]
    public async Task CreateAsync_FailureAfterAcceptance_ReplayReturnsSameObject()
    {
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var request = Request();
        var key = new PaymentSessionIdempotencyKey(
            request.OperationId,
            request.AttemptId,
            request.Revision);
        provider.FailOnce(FakeStripeSessionFaultPoint.AfterProviderAcceptance);
        var failed = await provider.CreateAsync(request, key);

        var replay = await provider.CreateAsync(request, key);
        Assert.True(replay.TryGetValue(out var replayed));
        var retrieved = await provider.RetrieveAsync(replayed.ProviderObjectKind, replayed.ProviderObjectId);

        Assert.True(failed.TryGetError(out var error));
        Assert.IsType<PaymentOperationError.ProviderUnavailable>(error);
        Assert.Equal(1, provider.ProviderObjectCount);
        Assert.True(retrieved.TryGetValue(out var observation));
        Assert.Equal(replayed, observation);
    }

    [Fact]
    public async Task CancelAsync_CancellableObject_ReturnsCanceledObservation()
    {
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var request = Request();
        var created = await provider.CreateAsync(
            request,
            new PaymentSessionIdempotencyKey(
                request.OperationId,
                request.AttemptId,
                request.Revision));
        Assert.True(created.TryGetValue(out var createdObservation));

        var canceled = await provider.CancelAsync(
            createdObservation.ProviderObjectKind,
            createdObservation.ProviderObjectId);

        Assert.True(canceled.TryGetValue(out var canceledObservation));
        Assert.Equal("canceled", canceledObservation.Status);
        Assert.True(canceledObservation.IsExplicitConsumerCancellation);
        Assert.False(canceledObservation.CanCancel);
    }

    private static PaymentSessionProviderRequest Request()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        return new(
            operationId,
            attemptId,
            1,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"booking:{operationId:N}",
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}",
            new Dictionary<string, string>());
    }
}
