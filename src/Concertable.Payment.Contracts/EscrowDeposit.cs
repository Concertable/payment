using Concertable.Payment.Contracts.Enums;

namespace Concertable.Payment.Contracts;

public sealed record EscrowDeposit(int EscrowId, string ChargeId, EscrowStatus Status, string? ClientSecret = null);
