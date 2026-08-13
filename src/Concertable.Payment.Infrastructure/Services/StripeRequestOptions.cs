using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeRequestOptions
{
    public static RequestOptions? Capture(Guid? operationId, Guid? commissionBindingId) =>
        Create(operationId, commissionBindingId, "capture");

    public static RequestOptions? Deposit(Guid? operationId, Guid? commissionBindingId) =>
        Create(operationId, commissionBindingId, "deposit");

    public static RequestOptions? Charge(Guid? commissionBindingId) =>
        Create(null, commissionBindingId, "charge");

    public static RequestOptions? HoldSession(Guid? commissionBindingId) =>
        Create(null, commissionBindingId, "hold-session");

    public static RequestOptions? Release(Guid? commissionBindingId) =>
        Create(null, commissionBindingId, "release");

    public static RequestOptions? Refund(Guid? operationId, Guid? commissionBindingId, long cumulativeGrossRefundMinor) =>
        operationId is not null
            ? Create(operationId, null, "refund")
            : Create(null, commissionBindingId, $"refund:{cumulativeGrossRefundMinor}");

    public static RequestOptions? RefundReversal(Guid? operationId, Guid? commissionBindingId, long cumulativeGrossRefundMinor) =>
        operationId is not null
            ? Create(operationId, null, "refund-reversal")
            : Create(null, commissionBindingId, $"refund-reversal:{cumulativeGrossRefundMinor}");

    private static RequestOptions? Create(Guid? operationId, Guid? commissionBindingId, string action)
    {
        if (operationId is not null && commissionBindingId is not null)
            throw new InvalidOperationException("A Stripe request cannot belong to both a financial operation and a commission binding.");

        var identity = operationId is not null
            ? $"operation:{operationId}"
            : commissionBindingId is not null
                ? $"commission:{commissionBindingId}"
                : null;

        return identity is null
            ? null
            : new RequestOptions { IdempotencyKey = $"{identity}:{action}" };
    }
}
