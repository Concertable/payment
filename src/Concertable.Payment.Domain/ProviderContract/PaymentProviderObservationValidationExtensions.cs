namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderObservationValidationExtensions
{
    extension(PaymentProviderObservation observation)
    {
        internal PaymentOperationTransitionRejection? ValidateIdentityAgainst(PaymentProviderAttempt current)
        {
            if (observation.OperationId != current.OperationId)
                return Reject(current, PaymentOperationTransitionRejectionReason.OperationMismatch);

            if (observation.AttemptId != current.AttemptId)
                return Reject(current, PaymentOperationTransitionRejectionReason.AttemptMismatch);

            if (observation.Revision < current.Revision)
                return Reject(current, PaymentOperationTransitionRejectionReason.StaleRevision);

            if (observation.Revision > current.Revision)
                return Reject(current, PaymentOperationTransitionRejectionReason.FutureRevision);

            return null;
        }

        internal PaymentOperationTransitionRejection? ValidateBindingAgainst(PaymentProviderAttempt current)
        {
            if (!observation.Context.HasSameProviderProductAs(current.Context)
                || !string.Equals(observation.ProviderObjectId, current.ProviderObjectId, StringComparison.Ordinal))
            {
                return Reject(current, PaymentOperationTransitionRejectionReason.ProviderObjectMismatch);
            }

            if (observation.Context != current.Context)
                return Reject(current, PaymentOperationTransitionRejectionReason.SessionKindMismatch);

            return null;
        }

        internal PaymentOperationTransitionRejection? ValidateContextAgainst(PaymentProviderAttempt current)
        {
            if (!current.Context.SupportsState(current.State))
                return Reject(current, PaymentOperationTransitionRejectionReason.InvalidCurrentStateForProviderObject);

            if (!observation.Context.SupportsState(observation.State))
            {
                return Reject(
                    current,
                    PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
                    observation.State);
            }

            if (observation.State == PaymentOperationState.Authorized && observation.CaptureBefore is null)
            {
                return Reject(
                    current,
                    PaymentOperationTransitionRejectionReason.CaptureDeadlineRequired,
                    observation.State);
            }

            if (observation.FailureCode is { } failureCode
                && (!Enum.IsDefined(failureCode)
                    || observation.State != PaymentOperationState.RequiresPaymentMethod))
            {
                return Reject(
                    current,
                    PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification,
                    observation.State);
            }

            return null;
        }

        internal PaymentOperationTransitionRejection? ValidateFreshnessAgainst(PaymentProviderAttempt current)
        {
            if (current.LastObservedAt is not { } lastObservedAt)
                return null;

            if (observation.ObservedAt < lastObservedAt)
            {
                return Reject(
                    current,
                    PaymentOperationTransitionRejectionReason.StaleObservation,
                    observation.State);
            }

            if (observation.ObservedAt == lastObservedAt
                && !string.Equals(observation.ProviderStatus, current.LastProviderStatus, StringComparison.Ordinal))
            {
                return Reject(
                    current,
                    PaymentOperationTransitionRejectionReason.AmbiguousObservationOrder,
                    observation.State);
            }

            return null;
        }
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentProviderAttempt current,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState? observedState = null) =>
        new(reason, current.State, observedState);
}
