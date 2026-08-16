namespace Concertable.Payment.Domain.ProviderContract;

internal enum PaymentAuthorizationExpiryDisposition
{
    NotDue,
    Reconcile,
    Expired
}

internal sealed record PaymentAuthorizationExpiryDecision(
    PaymentAuthorizationExpiryDisposition Disposition,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    PaymentOperationFailure? Failure);

internal static class PaymentAuthorizationExpiryEvaluator
{
    public static Result<PaymentAuthorizationExpiryDecision, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        DateTimeOffset observedAt,
        bool providerConfirmedUncaptured)
    {
        if (current.ProviderObjectKind != StripeProviderObjectKind.PaymentIntent
            || current.SessionKind != PaymentSessionKind.Authorization
            || current.State != PaymentOperationState.Authorized
            || current.CaptureBefore is null)
        {
            return new PaymentOperationTransitionRejection(
                PaymentOperationTransitionRejectionReason.InvalidAuthorizationExpiry,
                current.State);
        }

        if (observedAt < current.CaptureBefore.Value)
        {
            return new PaymentAuthorizationExpiryDecision(
                PaymentAuthorizationExpiryDisposition.NotDue,
                current.State,
                PaymentOperationTerminalDisposition.NonTerminal,
                PaymentOperationRetryDisposition.ContinueCurrentAttempt,
                null);
        }

        if (!providerConfirmedUncaptured)
        {
            return new PaymentAuthorizationExpiryDecision(
                PaymentAuthorizationExpiryDisposition.Reconcile,
                current.State,
                PaymentOperationTerminalDisposition.NonTerminal,
                PaymentOperationRetryDisposition.Reconcile,
                null);
        }

        return new PaymentAuthorizationExpiryDecision(
            PaymentAuthorizationExpiryDisposition.Expired,
            PaymentOperationState.Canceled,
            PaymentOperationTerminalDisposition.AttemptTerminal,
            PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Expired));
    }
}
