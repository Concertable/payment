using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal static class PaymentProviderAttemptExtensions
{
    private static readonly FrozenDictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        intentTransitions = new Dictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        {
            [PaymentOperationState.Creating] = Freeze(
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.RequiresPaymentMethod] = Freeze(
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.RequiresConfirmation] = Freeze(
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.RequiresAction] = Freeze(
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationState.Processing,
                PaymentOperationState.Authorized,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.Processing] = Freeze(
                PaymentOperationState.RequiresPaymentMethod,
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.Authorized] = Freeze(
                PaymentOperationState.Processing,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled)
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        refundTransitions = new Dictionary<PaymentOperationState, FrozenSet<PaymentOperationState>>
        {
            [PaymentOperationState.Creating] = Freeze(
                PaymentOperationState.Processing,
                PaymentOperationState.RequiresAction),
            [PaymentOperationState.Processing] = Freeze(
                PaymentOperationState.RequiresAction,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed),
            [PaymentOperationState.RequiresAction] = Freeze(
                PaymentOperationState.Processing,
                PaymentOperationState.Succeeded,
                PaymentOperationState.Canceled,
                PaymentOperationState.Failed)
        }.ToFrozenDictionary();

    extension(PaymentProviderAttempt current)
    {
        internal PaymentOperationTransitionRejection? ValidateObservation(StripeProviderObservation observation)
        {
            if (!string.Equals(observation.ApiVersion, StripeProviderContractBaseline.ApiVersion, StringComparison.Ordinal))
                return Reject(current, PaymentOperationTransitionRejectionReason.UnsupportedApiVersion);

            if (observation.OperationId != current.OperationId)
                return Reject(current, PaymentOperationTransitionRejectionReason.OperationMismatch);

            if (observation.AttemptId != current.AttemptId)
                return Reject(current, PaymentOperationTransitionRejectionReason.AttemptMismatch);

            if (observation.Revision < current.Revision)
                return Reject(current, PaymentOperationTransitionRejectionReason.StaleRevision);

            if (observation.Revision > current.Revision)
                return Reject(current, PaymentOperationTransitionRejectionReason.FutureRevision);

            if (observation.ProviderObjectKind != current.ProviderObjectKind
                || !string.Equals(observation.ProviderObjectId, current.ProviderObjectId, StringComparison.Ordinal))
            {
                return Reject(current, PaymentOperationTransitionRejectionReason.ProviderObjectMismatch);
            }

            if (observation.SessionKind != current.SessionKind)
                return Reject(current, PaymentOperationTransitionRejectionReason.SessionKindMismatch);

            if (!IsProviderObjectValidForSession(current.ProviderObjectKind, current.SessionKind))
                return Reject(current, PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind);

            if (!IsStateValidForProviderObject(current.State, current.ProviderObjectKind, current.SessionKind))
                return Reject(current, PaymentOperationTransitionRejectionReason.InvalidCurrentStateForProviderObject);

            return null;
        }

        internal bool HasSamePersistedProjectionAs(
            StripeProviderObservation observation,
            NormalizedProviderObservation normalized) =>
            current.State == normalized.State
            && string.Equals(current.LastProviderStatus, observation.Status, StringComparison.Ordinal)
            && current.LastObservedAt == observation.ObservedAt
            && current.CaptureBefore == observation.CaptureBefore
            && current.Failure == normalized.Failure;

        internal bool AllowsTransitionTo(PaymentOperationState next)
        {
            if (!IsStateValidForProviderObject(current.State, current.ProviderObjectKind, current.SessionKind)
                || !IsStateValidForProviderObject(next, current.ProviderObjectKind, current.SessionKind))
            {
                return false;
            }

            if (current.State == next)
                return true;

            var transitions = current.ProviderObjectKind == StripeProviderObjectKind.Refund
                ? refundTransitions
                : intentTransitions;

            return transitions.TryGetValue(current.State, out var allowed) && allowed.Contains(next);
        }
    }

    private static FrozenSet<PaymentOperationState> Freeze(params PaymentOperationState[] states) =>
        states.ToFrozenSet();

    private static bool IsProviderObjectValidForSession(
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind) =>
        providerObjectKind switch
        {
            StripeProviderObjectKind.PaymentIntent => sessionKind is PaymentSessionKind.Payment
                or PaymentSessionKind.Authorization,
            StripeProviderObjectKind.SetupIntent => sessionKind is PaymentSessionKind.PaymentMethodSetup
                or PaymentSessionKind.PaymentMethodVerification,
            StripeProviderObjectKind.Refund => sessionKind is null,
            _ => false
        };

    private static bool IsStateValidForProviderObject(
        PaymentOperationState state,
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind) =>
        providerObjectKind switch
        {
            StripeProviderObjectKind.PaymentIntent when sessionKind == PaymentSessionKind.Payment =>
                state != PaymentOperationState.Authorized,
            StripeProviderObjectKind.PaymentIntent when sessionKind == PaymentSessionKind.Authorization => true,
            StripeProviderObjectKind.SetupIntent => state != PaymentOperationState.Authorized,
            StripeProviderObjectKind.Refund => state is PaymentOperationState.Creating
                or PaymentOperationState.RequiresAction
                or PaymentOperationState.Processing
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled
                or PaymentOperationState.Failed,
            _ => false
        };

    private static PaymentOperationTransitionRejection Reject(
        PaymentProviderAttempt current,
        PaymentOperationTransitionRejectionReason reason) =>
        new(reason, current.State);
}
