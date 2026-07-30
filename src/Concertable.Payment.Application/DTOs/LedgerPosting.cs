namespace Concertable.Payment.Application.DTOs;

internal readonly record struct LedgerAccountRef(LedgerAccountType Type, Guid? OwnerId);

internal readonly record struct PostingLeg(LedgerAccountRef Account, LedgerDirection Direction, Money Amount);

internal sealed record LedgerPosting(
    LedgerPostingType PostingType,
    string ExternalId,
    int BookingId,
    string? PaymentIntentId,
    IReadOnlyList<PostingLeg> Legs);
