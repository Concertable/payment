using Concertable.Payment.Contracts;

namespace Concertable.Payment.Client;

public sealed record PaymentSessionDescriptor(
    PaymentOperationIdentity Identity,
    PaymentSessionKind Kind,
    string ClientSecret,
    string? CustomerSessionSecret,
    string? CustomerToken);
