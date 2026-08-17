namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentOperationTransitionEvaluator
{
    public static Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        PaymentProviderObservation observation)
    {
        var identityRejection = observation.ValidateIdentityAgainst(current);
        if (identityRejection is not null)
            return identityRejection;

        var providerBindingRejection = observation.ValidateBindingAgainst(current);
        if (providerBindingRejection is not null)
            return providerBindingRejection;

        var operationContextRejection = observation.ValidateContextAgainst(current);
        if (operationContextRejection is not null)
            return operationContextRejection;

        var freshnessRejection = observation.ValidateFreshnessAgainst(current);
        if (freshnessRejection is not null)
            return freshnessRejection;

        if (current.ToPersistedProjection() == observation.ToPersistedProjection())
            return observation.ToTransition(PaymentOperationTransitionDisposition.Duplicate);

        if (current.State.IsTerminal())
            return Reject(current, PaymentOperationTransitionRejectionReason.TerminalStateProtected, observation.State);

        if (!current.AllowsTransitionTo(observation.State))
            return Reject(current, PaymentOperationTransitionRejectionReason.IllegalTransition, observation.State);

        return observation.ToTransition(PaymentOperationTransitionDisposition.Applied);
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentProviderAttempt current,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState observedState) =>
        new(reason, current.State, observedState);
}
