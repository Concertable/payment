using System.Collections.Frozen;

namespace Concertable.Payment.Domain.ProviderContract;

internal static class StripeProviderObservationMappers
{
    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationTerminalDisposition>
        terminalDispositions = new Dictionary<PaymentOperationState, PaymentOperationTerminalDisposition>
        {
            [PaymentOperationState.Creating] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresConfirmation] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.RequiresAction] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Processing] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Authorized] = PaymentOperationTerminalDisposition.NonTerminal,
            [PaymentOperationState.Succeeded] = PaymentOperationTerminalDisposition.OperationTerminal,
            [PaymentOperationState.Canceled] = PaymentOperationTerminalDisposition.AttemptTerminal,
            [PaymentOperationState.Failed] = PaymentOperationTerminalDisposition.AttemptTerminal
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationRetryDisposition>
        retryDispositions = new Dictionary<PaymentOperationState, PaymentOperationRetryDisposition>
        {
            [PaymentOperationState.Creating] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationRetryDisposition.RetryCurrentAttempt,
            [PaymentOperationState.RequiresConfirmation] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.RequiresAction] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.Processing] = PaymentOperationRetryDisposition.Reconcile,
            [PaymentOperationState.Authorized] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [PaymentOperationState.Succeeded] = PaymentOperationRetryDisposition.NotRetryable,
            [PaymentOperationState.Canceled] = PaymentOperationRetryDisposition.NotRetryable,
            [PaymentOperationState.Failed] = PaymentOperationRetryDisposition.CreateNewAttempt
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<PaymentOperationState, PaymentOperationFailureCode> failures =
        new Dictionary<PaymentOperationState, PaymentOperationFailureCode>
        {
            [PaymentOperationState.RequiresPaymentMethod] = PaymentOperationFailureCode.PaymentMethodRequired,
            [PaymentOperationState.RequiresAction] = PaymentOperationFailureCode.AuthenticationRequired,
            [PaymentOperationState.Canceled] = PaymentOperationFailureCode.Canceled,
            [PaymentOperationState.Failed] = PaymentOperationFailureCode.Unknown
        }.ToFrozenDictionary();

    extension(StripeProviderObservation observation)
    {
        internal Result<NormalizedProviderObservation, PaymentOperationTransitionRejection> ToNormalized(
            PaymentProviderAttempt current)
        {
            if (!StripeProviderContractBaseline.NormalizedStates.TryGetValue(
                    observation.ProviderObjectKind,
                    out var states)
                || !states.TryGetValue(observation.Status, out var state))
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.UnknownProviderStatus,
                    current.State);
            }

            if (state == PaymentOperationState.Authorized
                && observation.SessionKind != PaymentSessionKind.Authorization)
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.InvalidProviderObjectForSessionKind,
                    current.State,
                    state);
            }

            if (state == PaymentOperationState.Authorized && observation.CaptureBefore is null)
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.CaptureDeadlineRequired,
                    current.State,
                    state);
            }

            if (observation.FailureClassification is { } failureClassification
                && (!Enum.IsDefined(failureClassification)
                    || state != PaymentOperationState.RequiresPaymentMethod))
            {
                return new PaymentOperationTransitionRejection(
                    PaymentOperationTransitionRejectionReason.InvalidProviderFailureClassification,
                    current.State,
                    state);
            }

            return new NormalizedProviderObservation(
                state,
                ToTerminalDisposition(state, observation.IsExplicitConsumerCancellation),
                retryDispositions[state],
                ToFailure(state, observation.FailureClassification));
        }

        internal PaymentOperationTransition ToTransition(
            PaymentOperationTransitionDisposition disposition,
            NormalizedProviderObservation normalized) =>
            new(
                disposition,
                normalized.State,
                observation.Status,
                observation.ObservedAt,
                observation.CaptureBefore,
                normalized.TerminalDisposition,
                normalized.RetryDisposition,
                normalized.Failure);
    }

    private static PaymentOperationTerminalDisposition ToTerminalDisposition(
        PaymentOperationState state,
        bool isExplicitConsumerCancellation) =>
        state == PaymentOperationState.Canceled && isExplicitConsumerCancellation
            ? PaymentOperationTerminalDisposition.OperationTerminal
            : terminalDispositions[state];

    private static PaymentOperationFailure? ToFailure(
        PaymentOperationState state,
        ProviderFailureClassification? failureClassification)
    {
        if (failureClassification == ProviderFailureClassification.Declined)
            return PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined);

        return failures.TryGetValue(state, out var code)
            ? PaymentOperationFailure.FromCode(code)
            : null;
    }
}
