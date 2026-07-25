namespace Concertable.Payment.Domain.Entities;

public readonly record struct LedgerLeg(LedgerAccountEntity Account, LedgerDirection Direction, Money Amount);
