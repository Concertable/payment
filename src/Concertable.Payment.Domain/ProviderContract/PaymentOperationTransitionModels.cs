namespace Concertable.Payment.Domain.ProviderContract;

internal enum ProviderFailureClassification
{
    Declined
}

internal abstract record PaymentProviderOperationContext
{
    private PaymentProviderOperationContext()
    {
    }

    internal sealed record Payment : PaymentProviderOperationContext;

    internal sealed record Authorization : PaymentProviderOperationContext;

    internal sealed record PaymentMethodSetup : PaymentProviderOperationContext;

    internal sealed record PaymentMethodVerification : PaymentProviderOperationContext;

    internal sealed record Refund : PaymentProviderOperationContext;
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
    PaymentProviderOperationContext Context,
    string ProviderObjectId,
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

internal sealed record PaymentProviderObservation(
    PaymentProviderOperationContext Context,
    string ProviderObjectId,
    Guid OperationId,
    Guid AttemptId,
    long Revision,
    PaymentOperationState State,
    string ProviderStatus,
    DateTimeOffset ObservedAt,
    DateTimeOffset? CaptureBefore,
    ProviderFailureClassification? FailureClassification,
    bool IsExplicitConsumerCancellation);

internal sealed record PaymentOperationTransition(
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
