namespace Concertable.Payment.Api.Identity;

internal interface ICurrentPayoutOwner
{
    Guid OwnerId { get; }
}
