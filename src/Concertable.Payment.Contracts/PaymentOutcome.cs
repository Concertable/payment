namespace Concertable.Payment.Contracts;

public sealed record PaymentOutcome
{
    public bool RequiresAction { get; init; }
    public string? ClientSecret { get; init; }
}
