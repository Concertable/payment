using Concertable.Payment.Infrastructure.Services;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeRequestOptionsTests
{
    private static readonly Guid OperationId = Guid.Parse("243e8198-041c-4e91-9828-c53dc5140546");
    private static readonly Guid CommissionBindingId = Guid.Parse("b13d389e-33bb-49e9-9771-e2f49afceb40");

    [Fact]
    public void Capture_FinancialOperation_ReturnsStableOperationKey()
    {
        var options = StripeRequestOptions.Capture(OperationId, null);

        Assert.Equal($"operation:{OperationId}:capture", options!.IdempotencyKey);
    }

    [Fact]
    public void Deposit_CommissionBinding_ReturnsStableCommissionKey()
    {
        var options = StripeRequestOptions.Deposit(null, CommissionBindingId);

        Assert.Equal($"commission:{CommissionBindingId}:deposit", options!.IdempotencyKey);
    }

    [Fact]
    public void Refund_FinancialOperation_DoesNotDependOnCumulativeAmount()
    {
        var options = StripeRequestOptions.Refund(OperationId, null, 5000);

        Assert.Equal($"operation:{OperationId}:refund", options!.IdempotencyKey);
    }

    [Fact]
    public void Refund_CommissionBinding_IncludesCumulativeAmount()
    {
        var options = StripeRequestOptions.Refund(null, CommissionBindingId, 5000);

        Assert.Equal($"commission:{CommissionBindingId}:refund:5000", options!.IdempotencyKey);
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
