namespace Concertable.Payment.Contracts;

public record PaymentResponse
{
    public bool RequiresAction { get; set; }
    public string? ClientSecret { get; set; }
    public string? TransactionId { get; set; }
}
