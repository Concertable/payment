using Concertable.Payment.Domain;

namespace Concertable.Payment.Client;

public sealed record EscrowResponse(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);
