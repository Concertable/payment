using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts.Events;

[MessageType("concertable.payment.payment-operation-state-changed.v1")]
public sealed record PaymentOperationStateChangedV1(
    PaymentOperationIdentity Identity,
    PaymentSessionKind SessionKind,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    PaymentOperationFailure? Failure,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CaptureBefore,
    DateTimeOffset ObservedAt) : IIntegrationEvent;
