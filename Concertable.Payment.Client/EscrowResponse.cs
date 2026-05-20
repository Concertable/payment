using Concertable.Payment.Domain;

namespace Concertable.Payment.Client;

public record EscrowResponse(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);
