namespace Concertable.Payment.Client;

public sealed record PaymentMethodSetup(
    string ClientSecret,
    string? CustomerSessionSecret,
    string? CustomerToken);
