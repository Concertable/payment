using Concertable.Kernel.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Concertable.Payment.Api.Identity;

internal sealed class CurrentPayoutOwner : ICurrentPayoutOwner
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentPayoutOwner(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }

    public Guid OwnerId =>
        httpContextAccessor.HttpContext?.User.FindFirst("owner") is { } claim
        && Guid.TryParse(claim.Value, out var ownerId)
            ? ownerId
            : throw new UnauthorizedException("No owner claim on the current principal.");
}
