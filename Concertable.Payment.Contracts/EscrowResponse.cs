namespace Concertable.Payment.Contracts;

public record EscrowResponse(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);
