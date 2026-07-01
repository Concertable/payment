namespace Concertable.Payment.Client;

public record PaymentResponse
{
    public bool RequiresAction { get; init; }
    public string? ClientSecret { get; init; }
    public string? TransactionId { get; init; }
}
