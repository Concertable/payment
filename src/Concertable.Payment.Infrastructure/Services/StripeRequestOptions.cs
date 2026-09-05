using Concertable.Payment.Application.Provider;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal static class StripeRequestOptions
{
    public static RequestOptions? Capture(Guid? operationId, Guid? commissionBindingId) =>
        Create(operationId, commissionBindingId, null, "capture");

    public static RequestOptions? Deposit(Guid? operationId, Guid? commissionBindingId) =>
        Create(operationId, commissionBindingId, null, "deposit");

    public static RequestOptions? Charge(Guid? operationId, Guid? commissionBindingId) =>
        operationId is not null
            ? Create(operationId, null, null, "charge")
            : Create(null, commissionBindingId, null, "charge");

    public static RequestOptions? HoldSession(Guid? commissionBindingId) =>
        Create(null, commissionBindingId, null, "hold-session");

    public static RequestOptions? Release(Guid? operationId, Guid? commissionBindingId) =>
        operationId is not null
            ? Create(operationId, null, null, "release")
            : Create(null, commissionBindingId, null, "release");

    public static RequestOptions? Refund(Guid? operationId, Guid? commissionBindingId, Guid refundId) =>
        operationId is not null
            ? Create(operationId, null, refundId, "refund")
            : Create(null, commissionBindingId, refundId, "refund");

    public static RequestOptions? RefundReversal(Guid? operationId, Guid? commissionBindingId, Guid refundId) =>
        operationId is not null
            ? Create(operationId, null, refundId, "refund-reversal")
            : Create(null, commissionBindingId, refundId, "refund-reversal");

    private static RequestOptions? Create(
        Guid? operationId,
        Guid? commissionBindingId,
        Guid? attemptId,
        string action)
    {
        if (operationId is not null && commissionBindingId is not null)
            throw new InvalidOperationException("A Stripe request cannot belong to both a financial operation and a commission binding.");

        if (operationId is { } operation)
            return Options(StripeIdempotencyScope.FinancialOperation, operation, attemptId, action);

        return commissionBindingId is { } binding
            ? Options(StripeIdempotencyScope.CommissionBinding, binding, attemptId, action)
            : null;
    }

    private static RequestOptions Options(
        StripeIdempotencyScope scope,
        Guid identityId,
        Guid? attemptId,
        string action)
    {
        var key = attemptId is { } attempt
            ? StripeIdempotencyKey.ForAttempt(scope, identityId, attempt, action)
            : StripeIdempotencyKey.ForSingleAttempt(scope, identityId, action);

        return new RequestOptions { IdempotencyKey = key.ToString() };
    }
}
