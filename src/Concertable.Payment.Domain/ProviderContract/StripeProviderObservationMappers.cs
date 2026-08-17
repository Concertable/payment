using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal static class StripeProviderObservationMappers
{
    private static readonly FrozenDictionary<
        (StripeProviderObjectKind ProviderObjectKind, PaymentSessionKind? SessionKind),
        PaymentProviderOperationContext> operationContexts =
        new Dictionary<
            (StripeProviderObjectKind ProviderObjectKind, PaymentSessionKind? SessionKind),
            PaymentProviderOperationContext>
        {
            [(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Payment)] =
                new PaymentProviderOperationContext.Payment(),
            [(StripeProviderObjectKind.PaymentIntent, PaymentSessionKind.Authorization)] =
                new PaymentProviderOperationContext.Authorization(),
            [(StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodSetup)] =
                new PaymentProviderOperationContext.PaymentMethodSetup(),
            [(StripeProviderObjectKind.SetupIntent, PaymentSessionKind.PaymentMethodVerification)] =
                new PaymentProviderOperationContext.PaymentMethodVerification(),
            [(StripeProviderObjectKind.Refund, null)] = new PaymentProviderOperationContext.Refund()
        }.ToFrozenDictionary();

    extension(StripeProviderObservation observation)
    {
        internal Result<PaymentProviderObservation, PaymentOperationTransitionRejection> ToNormalized(
            PaymentOperationState currentState)
        {
            if (!string.Equals(
                    observation.ApiVersion,
                    StripeProviderContractBaseline.ApiVersion,
                    StringComparison.Ordinal))
            {
                return Reject(PaymentOperationTransitionRejectionReason.UnsupportedApiVersion, currentState);
            }

            if (!StripeProviderContractBaseline.NormalizedStates.TryGetValue(
                    observation.ProviderObjectKind,
                    out var states)
                || !states.TryGetValue(observation.Status, out var state))
            {
                return Reject(PaymentOperationTransitionRejectionReason.UnknownProviderStatus, currentState);
            }

            if (!operationContexts.TryGetValue(
                    (observation.ProviderObjectKind, observation.SessionKind),
                    out var context))
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
                    currentState,
                    state);
            }

            if (observation.FailureClassification is { } failureClassification
                && (!Enum.IsDefined(failureClassification)
                    || state != PaymentOperationState.RequiresPaymentMethod))
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification,
                    currentState,
                    state);
            }

            return new PaymentProviderObservation(
                context,
                observation.ProviderObjectId,
                observation.OperationId,
                observation.AttemptId,
                observation.Revision,
                state,
                observation.Status,
                observation.ObservedAt,
                observation.CaptureBefore,
                observation.FailureClassification,
                observation.IsExplicitConsumerCancellation);
        }
    }

    private static PaymentOperationTransitionRejection Reject(
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState currentState) =>
        new(reason, currentState);
}
