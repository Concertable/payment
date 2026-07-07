namespace Concertable.Payment.Contracts;

public record PaymentOutcome
{
    public bool RequiresAction { get; init; }
    public string? ClientSecret { get; init; }
    public string? TransactionId { get; init; }
}
