using Concertable.Payment.Infrastructure.Services;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeRequestOptionsTests
{
    private static readonly Guid OperationId = Guid.Parse("243e8198-041c-4e91-9828-c53dc5140546");
    private static readonly Guid CommissionBindingId = Guid.Parse("b13d389e-33bb-49e9-9771-e2f49afceb40");
    private static readonly Guid RefundId = Guid.Parse("7c2b1f0a-9d64-4a0c-8f31-0c8d5e6a1b42");
    private static readonly Guid OtherRefundId = Guid.Parse("1a9f4c37-2b58-4d6e-9a10-5f7c3e8d2b91");

    [Fact]
    public void Capture_FinancialOperation_ReturnsStableOperationKey()
    {
        var options = StripeRequestOptions.Capture(OperationId, null);

        Assert.Equal(
            $"financial-operation:{OperationId:D}:{OperationId:D}:1:capture",
            options!.IdempotencyKey);
    }

    [Fact]
    public void Deposit_CommissionBinding_ReturnsStableCommissionKey()
    {
        var options = StripeRequestOptions.Deposit(null, CommissionBindingId);

        Assert.Equal(
            $"commission-binding:{CommissionBindingId:D}:{CommissionBindingId:D}:1:deposit",
            options!.IdempotencyKey);
    }

    [Fact]
    public void Refund_FinancialOperation_KeysOnTheRefundAttempt()
    {
        var options = StripeRequestOptions.Refund(OperationId, null, RefundId);

        Assert.Equal(
            $"financial-operation:{OperationId:D}:{RefundId:D}:1:refund",
            options!.IdempotencyKey);
    }

    [Fact]
    public void Refund_CommissionBinding_SeparatesRefundAttemptsOnOneBinding()
    {
        var first = StripeRequestOptions.Refund(null, CommissionBindingId, RefundId);
        var second = StripeRequestOptions.Refund(null, CommissionBindingId, OtherRefundId);

        Assert.Equal(
            $"commission-binding:{CommissionBindingId:D}:{RefundId:D}:1:refund",
            first!.IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, second!.IdempotencyKey);
    }

    [Fact]
    public void RefundReversal_SameRefundAttempt_DiffersFromTheRefundKey()
    {
        var refund = StripeRequestOptions.Refund(OperationId, null, RefundId);
        var reversal = StripeRequestOptions.RefundReversal(OperationId, null, RefundId);

        Assert.NotEqual(refund!.IdempotencyKey, reversal!.IdempotencyKey);
    }

    [Fact]
    public void Capture_WithoutBusinessIdentity_ReturnsNoOptions()
    {
        var options = StripeRequestOptions.Capture(null, null);

        Assert.Null(options);
    }

    [Fact]
    public void Capture_WithTwoBusinessIdentities_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StripeRequestOptions.Capture(OperationId, CommissionBindingId));
    }
}
