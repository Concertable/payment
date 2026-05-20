using Concertable.User.Contracts.Events;
using Concertable.Payment.Application.Interfaces;
using Concertable.Shared;

namespace Concertable.Payment.Infrastructure.Handlers;

internal class ManagerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    : IIntegrationEventHandler<VenueManagerRegisteredEvent>,
      IIntegrationEventHandler<ArtistManagerRegisteredEvent>
{
    public async Task HandleAsync(VenueManagerRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        await stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.UserId, e.Email, ct);
    }

    public async Task HandleAsync(ArtistManagerRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        await stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.UserId, e.Email, ct);
    }
}
