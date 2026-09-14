namespace Concertable.Payment.Domain.Events;

internal sealed record PaymentOperationStateChangedDomainEvent(
    PaymentOperationIdentity Identity,
    PaymentSessionKind SessionKind,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    PaymentOperationFailure? Failure,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CaptureBefore,
    DateTimeOffset ObservedAt) : IDomainEvent;
