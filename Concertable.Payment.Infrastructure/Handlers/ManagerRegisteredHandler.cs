using Concertable.Auth.Contracts;
using Concertable.Auth.Contracts.Events;
using Concertable.Payment.Application.Interfaces;
using Concertable.Shared;

namespace Concertable.Payment.Infrastructure.Handlers;

internal class ManagerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    : IIntegrationEventHandler<CredentialRegisteredEvent>
{
    public async Task HandleAsync(CredentialRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        if (e.ClientId is not (ClientIds.VenueWeb or ClientIds.VenueMobile or ClientIds.ArtistWeb or ClientIds.ArtistMobile))
            return;

        await stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.UserId, e.Email, ct);
    }
}
