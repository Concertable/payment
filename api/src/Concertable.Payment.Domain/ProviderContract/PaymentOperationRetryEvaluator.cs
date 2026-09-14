namespace Concertable.Payment.Domain.ProviderContract;

internal enum PaymentOperationRetryTrigger
{
    ExplicitConsumerRetry,
    TransportRetry,
    TimeoutRecovery,
    WebhookRedelivery,
    Reconciliation
}

internal sealed record PaymentOperationRetryRequest(
    PaymentOperationRetryTrigger Trigger,
    string RequestFingerprint,
    Guid? ProposedAttemptId = null);

internal sealed record PaymentOperationRetryDecision(
    PaymentOperationRetryDisposition Disposition,
    Guid AttemptId,
    long Revision);

internal static class PaymentOperationRetryEvaluator
{
    public static Result<PaymentOperationRetryDecision, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        PaymentOperationRetryRequest request)
    {
        if (!Enum.IsDefined(request.Trigger))
        {
            return new PaymentOperationTransitionRejection(
                PaymentOperationTransitionRejectionReason.UnknownRetryTrigger,
                current.State);
        }

        if (!string.Equals(current.RequestFingerprint, request.RequestFingerprint, StringComparison.Ordinal))
        {
            return new PaymentOperationTransitionRejection(
                PaymentOperationTransitionRejectionReason.ImmutableBindingMismatch,
                current.State);
        }

        if (request.Trigger != PaymentOperationRetryTrigger.ExplicitConsumerRetry)
        {
            var disposition = request.Trigger switch
            {
                PaymentOperationRetryTrigger.TransportRetry or PaymentOperationRetryTrigger.TimeoutRecovery =>
                    PaymentOperationRetryDisposition.RetryCurrentAttempt,
                PaymentOperationRetryTrigger.WebhookRedelivery =>
                    PaymentOperationRetryDisposition.ContinueCurrentAttempt,
                PaymentOperationRetryTrigger.Reconciliation => PaymentOperationRetryDisposition.Reconcile,
                _ => PaymentOperationRetryDisposition.NotRetryable
            };

            return new PaymentOperationRetryDecision(disposition, current.AttemptId, current.Revision);
        }

        var retryable = current.State == PaymentOperationState.Failed
            || current.State == PaymentOperationState.Canceled
            && current.Failure?.Code == PaymentOperationFailureCode.Expired;

        if (!retryable)
        {
            return new PaymentOperationRetryDecision(
                PaymentOperationRetryDisposition.NotRetryable,
                current.AttemptId,
                current.Revision);
        }

        if (request.ProposedAttemptId is not { } proposedAttemptId
            || proposedAttemptId == Guid.Empty
            || proposedAttemptId == current.AttemptId)
        {
            return new PaymentOperationTransitionRejection(
                PaymentOperationTransitionRejectionReason.InvalidRetryAttempt,
                current.State);
        }

        return new PaymentOperationRetryDecision(
            PaymentOperationRetryDisposition.CreateNewAttempt,
            proposedAttemptId,
            checked(current.Revision + 1));
    }
}
