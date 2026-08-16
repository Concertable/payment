namespace Concertable.Payment.Domain.ProviderContract;

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

internal sealed record NormalizedProviderObservation(
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    PaymentOperationFailure? Failure);

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
