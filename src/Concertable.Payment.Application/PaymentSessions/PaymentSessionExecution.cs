namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionExecution(
    PaymentOperationIdentity Identity,
    PaymentSessionKind Kind,
    PaymentOperationState State,
    string? ClientSecret,
    string? CustomerSessionSecret,
    string? CustomerToken);
