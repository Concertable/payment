using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.PaymentSessions;
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
        await Assert.ThrowsAsync<PaymentSessionProviderUnavailableException>(
            () => provider.CreateAsync(request, key));

        var replay = await provider.CreateAsync(request, key);

        Assert.Equal(1, provider.ProviderObjectCount);
        Assert.Equal(replay, await provider.RetrieveAsync(replay.ProviderObjectKind, replay.ProviderObjectId));
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

        var canceled = await provider.CancelAsync(created.ProviderObjectKind, created.ProviderObjectId);

        Assert.Equal("canceled", canceled.Status);
        Assert.True(canceled.IsExplicitConsumerCancellation);
        Assert.False(canceled.CanCancel);
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
            "escrow",
            $"booking:{operationId:N}",
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"cus_{operationId:N}",
            $"acct_{operationId:N}",
            new Dictionary<string, string>());
    }
}
