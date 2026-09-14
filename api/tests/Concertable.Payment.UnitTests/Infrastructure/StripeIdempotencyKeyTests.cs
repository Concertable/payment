using Concertable.Payment.Application.Provider;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeIdempotencyKeyTests
{
    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();

        var first = StripeIdempotencyKey.ForSessionAttempt(operationId, attemptId, 1);
        var second = StripeIdempotencyKey.ForSessionAttempt(operationId, attemptId, 1);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ForSessionAttempt_NewRevision_ReturnsDistinctKey()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();

        var first = StripeIdempotencyKey.ForSessionAttempt(operationId, attemptId, 1);
        var second = StripeIdempotencyKey.ForSessionAttempt(operationId, Guid.CreateVersion7(), 2);

        Assert.NotEqual(first.ToString(), second.ToString());
    }

    [Fact]
    public void ForSessionAttempt_ValidComponents_ReturnsCanonicalKey()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();

        var value = StripeIdempotencyKey.ForSessionAttempt(operationId, attemptId, 1).ToString();

        Assert.Equal($"payment-session:{operationId:D}:{attemptId:D}:1:create", value);
    }

    [Fact]
    public void ForSingleAttempt_ValidComponents_RepeatsTheIdentityAsTheAttempt()
    {
        var operationId = Guid.CreateVersion7();

        var value = StripeIdempotencyKey
            .ForSingleAttempt(StripeIdempotencyScope.FinancialOperation, operationId, "charge")
            .ToString();

        Assert.Equal($"financial-operation:{operationId:D}:{operationId:D}:1:charge", value);
    }

    [Fact]
    public void ForAttempt_ValidComponents_ReturnsCanonicalKey()
    {
        var bindingId = Guid.CreateVersion7();
        var refundId = Guid.CreateVersion7();

        var value = StripeIdempotencyKey
            .ForAttempt(StripeIdempotencyScope.CommissionBinding, bindingId, refundId, "refund")
            .ToString();

        Assert.Equal($"commission-binding:{bindingId:D}:{refundId:D}:1:refund", value);
    }
}
