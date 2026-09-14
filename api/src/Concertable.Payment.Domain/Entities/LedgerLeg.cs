namespace Concertable.Payment.Domain.Entities;

internal readonly record struct LedgerLeg(LedgerAccountEntity Account, LedgerDirection Direction, Money Amount);
