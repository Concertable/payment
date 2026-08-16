namespace Concertable.Payment.Domain.ProviderContract;

internal static class StripeProviderContractBaseline
{
    public const string StripeNetVersion = "47.3.0";
    public const string ApiVersion = "2025-01-27.acacia";

    public static readonly IReadOnlyList<string> PaymentIntentStatuses =
    [
        "requires_payment_method",
        "requires_confirmation",
        "requires_action",
        "processing",
        "requires_capture",
        "canceled",
        "succeeded"
    ];

    public static readonly IReadOnlyList<string> SetupIntentStatuses =
    [
        "requires_payment_method",
        "requires_confirmation",
        "requires_action",
        "processing",
        "canceled",
        "succeeded"
    ];

    public static readonly IReadOnlyList<string> RefundStatuses =
    [
        "pending",
        "requires_action",
        "succeeded",
        "failed",
        "canceled"
    ];
}

internal enum StripeProviderObjectKind
{
    PaymentIntent,
    SetupIntent,
    Refund
}

internal enum ProviderFailureClassification
{
    Declined
}

internal enum PaymentOperationTransitionDisposition
{
    Applied,
    Duplicate
}

internal enum PaymentOperationTransitionRejectionReason
{
    UnsupportedApiVersion,
    UnknownProviderStatus,
    OperationMismatch,
    AttemptMismatch,
    StaleRevision,
    FutureRevision,
    ProviderObjectMismatch,
    SessionKindMismatch,
    InvalidProviderObjectForSessionKind,
    InvalidCurrentStateForProviderObject,
    CaptureDeadlineRequired,
    StaleObservation,
    AmbiguousObservationOrder,
    TerminalStateProtected,
    IllegalTransition,
    ImmutableBindingMismatch,
    InvalidRetryAttempt,
    UnknownRetryTrigger,
    InvalidAuthorizationExpiry,
    InvalidProviderFailureClassification
}

internal sealed record PaymentProviderAttempt(
    Guid OperationId,
    Guid AttemptId,
    long Revision,
    StripeProviderObjectKind ProviderObjectKind,
    string ProviderObjectId,
    PaymentSessionKind? SessionKind,
    PaymentOperationState State,
    string RequestFingerprint,
    string? LastProviderStatus = null,
    DateTimeOffset? LastObservedAt = null,
    DateTimeOffset? CaptureBefore = null,
    PaymentOperationFailure? Failure = null);

internal sealed record StripeProviderObservation(
    string ApiVersion,
    StripeProviderObjectKind ProviderObjectKind,
    string ProviderObjectId,
    Guid OperationId,
    Guid AttemptId,
    long Revision,
    PaymentSessionKind? SessionKind,
    string Status,
    DateTimeOffset ObservedAt,
    DateTimeOffset? CaptureBefore = null,
    ProviderFailureClassification? FailureClassification = null,
    bool IsExplicitConsumerCancellation = false);

internal sealed record PaymentOperationTransition(
    PaymentOperationTransitionDisposition Disposition,
    PaymentOperationState State,
    string ProviderStatus,
    DateTimeOffset ObservedAt,
    DateTimeOffset? CaptureBefore,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    PaymentOperationFailure? Failure);

internal sealed record PaymentOperationTransitionRejection(
    PaymentOperationTransitionRejectionReason Reason,
    PaymentOperationState CurrentState,
    PaymentOperationState? ObservedState = null);

internal static class StripeOperationTransitionSpecification
{
    public static Result<PaymentOperationTransition, PaymentOperationTransitionRejection> Evaluate(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        var identityRejection = ValidateIdentity(current, observation);
        if (identityRejection is not null)
            return identityRejection;

        var normalized = Normalize(current, observation);
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

        if (IsPersistedProjectionUnchanged(current, observation, observed))
            return CreateTransition(PaymentOperationTransitionDisposition.Duplicate, observation, observed);

        if (IsTerminal(current.State))
            return Reject(current, PaymentOperationTransitionRejectionReason.TerminalStateProtected, observed.State);

        if (!IsAllowedSameRevisionTransition(
                current.State,
                observed.State,
                current.ProviderObjectKind,
                current.SessionKind))
            return Reject(current, PaymentOperationTransitionRejectionReason.IllegalTransition, observed.State);

        return CreateTransition(PaymentOperationTransitionDisposition.Applied, observation, observed);
    }

    private static bool IsPersistedProjectionUnchanged(
        PaymentProviderAttempt current,
        StripeProviderObservation observation,
        NormalizedProviderObservation normalized) =>
        current.State == normalized.State
        && string.Equals(current.LastProviderStatus, observation.Status, StringComparison.Ordinal)
        && current.LastObservedAt == observation.ObservedAt
        && current.CaptureBefore == observation.CaptureBefore
        && current.Failure == normalized.Failure;

    internal static bool IsAllowedSameRevisionTransition(
        PaymentOperationState current,
        PaymentOperationState next,
        StripeProviderObjectKind providerObjectKind,
        PaymentSessionKind? sessionKind)
    {
        if (!IsStateValidForProviderObject(current, providerObjectKind, sessionKind)
            || !IsStateValidForProviderObject(next, providerObjectKind, sessionKind))
        {
            return false;
        }

        if (current == next)
            return true;

        if (providerObjectKind == StripeProviderObjectKind.Refund)
        {
            return current switch
            {
                PaymentOperationState.Creating => next is PaymentOperationState.Processing
                    or PaymentOperationState.RequiresAction,
                PaymentOperationState.Processing => next is PaymentOperationState.RequiresAction
                    or PaymentOperationState.Succeeded
                    or PaymentOperationState.Canceled
                    or PaymentOperationState.Failed,
                PaymentOperationState.RequiresAction => next is PaymentOperationState.Processing
                    or PaymentOperationState.Succeeded
                    or PaymentOperationState.Canceled
                    or PaymentOperationState.Failed,
                _ => false
            };
        }

        return current switch
        {
            PaymentOperationState.Creating => next is not PaymentOperationState.Creating,
            PaymentOperationState.RequiresPaymentMethod => next is PaymentOperationState.RequiresConfirmation
                or PaymentOperationState.RequiresAction
                or PaymentOperationState.Processing
                or PaymentOperationState.Authorized
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled
                or PaymentOperationState.Failed,
            PaymentOperationState.RequiresConfirmation => next is PaymentOperationState.RequiresPaymentMethod
                or PaymentOperationState.RequiresAction
                or PaymentOperationState.Processing
                or PaymentOperationState.Authorized
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled
                or PaymentOperationState.Failed,
            PaymentOperationState.RequiresAction => next is PaymentOperationState.RequiresPaymentMethod
                or PaymentOperationState.RequiresConfirmation
                or PaymentOperationState.Processing
                or PaymentOperationState.Authorized
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled
                or PaymentOperationState.Failed,
            PaymentOperationState.Processing => next is PaymentOperationState.RequiresPaymentMethod
                or PaymentOperationState.RequiresAction
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled
                or PaymentOperationState.Failed,
            PaymentOperationState.Authorized => next is PaymentOperationState.Processing
                or PaymentOperationState.Succeeded
                or PaymentOperationState.Canceled,
            _ => false
        };
    }

    private static PaymentOperationTransitionRejection? ValidateIdentity(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
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

    private static Result<NormalizedProviderObservation, PaymentOperationTransitionRejection> Normalize(
        PaymentProviderAttempt current,
        StripeProviderObservation observation)
    {
        PaymentOperationState? state = observation.ProviderObjectKind switch
        {
            StripeProviderObjectKind.PaymentIntent => observation.Status switch
            {
                "requires_payment_method" => PaymentOperationState.RequiresPaymentMethod,
                "requires_confirmation" => PaymentOperationState.RequiresConfirmation,
                "requires_action" => PaymentOperationState.RequiresAction,
                "processing" => PaymentOperationState.Processing,
                "requires_capture" => PaymentOperationState.Authorized,
                "succeeded" => PaymentOperationState.Succeeded,
                "canceled" => PaymentOperationState.Canceled,
                _ => null
            },
            StripeProviderObjectKind.SetupIntent => observation.Status switch
            {
                "requires_payment_method" => PaymentOperationState.RequiresPaymentMethod,
                "requires_confirmation" => PaymentOperationState.RequiresConfirmation,
                "requires_action" => PaymentOperationState.RequiresAction,
                "processing" => PaymentOperationState.Processing,
                "succeeded" => PaymentOperationState.Succeeded,
                "canceled" => PaymentOperationState.Canceled,
                _ => null
            },
            StripeProviderObjectKind.Refund => observation.Status switch
            {
                "pending" => PaymentOperationState.Processing,
                "requires_action" => PaymentOperationState.RequiresAction,
                "succeeded" => PaymentOperationState.Succeeded,
                "failed" => PaymentOperationState.Failed,
                "canceled" => PaymentOperationState.Canceled,
                _ => null
            },
            _ => null
        };

        if (state is null)
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
            state.Value,
            TerminalDisposition(state.Value, observation.IsExplicitConsumerCancellation),
            RetryDisposition(state.Value),
            Failure(state.Value, observation.FailureClassification));
    }

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

    private static bool IsTerminal(PaymentOperationState state) =>
        state is PaymentOperationState.Succeeded
            or PaymentOperationState.Canceled
            or PaymentOperationState.Failed;

    private static PaymentOperationTerminalDisposition TerminalDisposition(
        PaymentOperationState state,
        bool isExplicitConsumerCancellation) =>
        state switch
        {
            PaymentOperationState.Succeeded => PaymentOperationTerminalDisposition.OperationTerminal,
            PaymentOperationState.Canceled when isExplicitConsumerCancellation =>
                PaymentOperationTerminalDisposition.OperationTerminal,
            PaymentOperationState.Canceled or PaymentOperationState.Failed =>
                PaymentOperationTerminalDisposition.AttemptTerminal,
            _ => PaymentOperationTerminalDisposition.NonTerminal
        };

    private static PaymentOperationRetryDisposition RetryDisposition(PaymentOperationState state) =>
        state switch
        {
            PaymentOperationState.RequiresPaymentMethod => PaymentOperationRetryDisposition.RetryCurrentAttempt,
            PaymentOperationState.RequiresAction => PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            PaymentOperationState.Processing => PaymentOperationRetryDisposition.Reconcile,
            PaymentOperationState.Failed => PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationState.Succeeded or PaymentOperationState.Canceled =>
                PaymentOperationRetryDisposition.NotRetryable,
            _ => PaymentOperationRetryDisposition.ContinueCurrentAttempt
        };

    private static PaymentOperationFailure? Failure(
        PaymentOperationState state,
        ProviderFailureClassification? failureClassification) =>
        (state, failureClassification) switch
        {
            (PaymentOperationState.RequiresPaymentMethod, ProviderFailureClassification.Declined) =>
                new PaymentOperationFailure(
                    PaymentOperationFailureCode.Declined,
                    "The payment was declined."),
            (PaymentOperationState.RequiresPaymentMethod, null) => new PaymentOperationFailure(
                PaymentOperationFailureCode.PaymentMethodRequired,
                "A usable payment method is required."),
            (PaymentOperationState.RequiresAction, null) => new PaymentOperationFailure(
                PaymentOperationFailureCode.AuthenticationRequired,
                "Payment authentication is required."),
            (PaymentOperationState.Canceled, null) => new PaymentOperationFailure(
                PaymentOperationFailureCode.Canceled,
                "The payment operation was canceled."),
            (PaymentOperationState.Failed, null) => new PaymentOperationFailure(
                PaymentOperationFailureCode.Unknown,
                "The payment state could not be safely classified."),
            (_, null) => null,
            _ => throw new InvalidOperationException("The provider failure classification was not normalized.")
        };

    private static PaymentOperationTransition CreateTransition(
        PaymentOperationTransitionDisposition disposition,
        StripeProviderObservation observation,
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

    private static PaymentOperationTransitionRejection Reject(
        PaymentProviderAttempt current,
        PaymentOperationTransitionRejectionReason reason,
        PaymentOperationState? observedState = null) =>
        new(reason, current.State, observedState);

    private sealed record NormalizedProviderObservation(
        PaymentOperationState State,
        PaymentOperationTerminalDisposition TerminalDisposition,
        PaymentOperationRetryDisposition RetryDisposition,
        PaymentOperationFailure? Failure);
}
