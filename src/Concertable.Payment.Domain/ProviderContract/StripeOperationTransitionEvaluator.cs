namespace Concertable.Payment.Domain.ProviderContract;

internal static class StripeOperationTransitionEvaluator
{
    public static Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        var identityRejection = current.ValidateObservation(observation);
        if (identityRejection is not null)
            return identityRejection;

        var normalized = observation.ToNormalized(current);
        if (!normalized.TryGetValue(out var observed))
        {
            if (normalized.TryGetError(out var rejection))
                return rejection;

            throw new InvalidOperationException("The provider normalization result was uninitialized.");
        }

        if (current.LastObservedAt is { } lastObservedAt)
        {
            if (observation.ObservedAt < lastObservedAt)
                return Reject(current, PaymentOperationTransitionRejectionReason.StaleObservation, observed.State);

            if (observation.ObservedAt == lastObservedAt
                && !string.Equals(observation.Status, current.LastProviderStatus, StringComparison.Ordinal))
            {
                return Reject(current, PaymentOperationTransitionRejectionReason.AmbiguousObservationOrder, observed.State);
            }
        }

        if (current.HasSamePersistedProjectionAs(observation, observed))
            return observation.ToTransition(PaymentOperationTransitionDisposition.Duplicate, observed);

        if (current.State.IsTerminal())
            return Reject(current, PaymentOperationTransitionRejectionReason.TerminalStateProtected, observed.State);

        if (!current.AllowsTransitionTo(observed.State))
            return Reject(current, PaymentOperationTransitionRejectionReason.IllegalTransition, observed.State);

        return observation.ToTransition(PaymentOperationTransitionDisposition.Applied, observed);
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentProviderAttempt current,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState? observedState = null) =>
        new(reason, current.State, observedState);
}
