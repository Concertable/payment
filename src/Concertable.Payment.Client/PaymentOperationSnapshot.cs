using Concertable.Payment.Contracts;

namespace Concertable.Payment.Client;

public sealed record PaymentOperationSnapshot(
    PaymentOperationIdentity Identity,
    PaymentOperationState State,
    PaymentOperationTerminalDisposition TerminalDisposition,
    PaymentOperationRetryDisposition RetryDisposition,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? CaptureBefore,
    PaymentOperationFailure? Failure);
