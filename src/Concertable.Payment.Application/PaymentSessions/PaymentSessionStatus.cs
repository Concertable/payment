namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionStatus(
    PaymentOperationIdentity Identity,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CaptureBefore,
    PaymentOperationFailure? Failure);
