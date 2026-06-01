using Concertable.Auth.Contracts;
using Concertable.Auth.Contracts.Events;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class ManagerRegisteredHandler : IIntegrationEventHandler<CredentialRegisteredEvent>
{
    private readonly IStripeAccountClient stripeAccountClient;

    public ManagerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    {
        this.stripeAccountClient = stripeAccountClient;
    }

    public async Task HandleAsync(CredentialRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        if (e.ClientId is not (ClientIds.VenueWeb or ClientIds.VenueMobile or ClientIds.ArtistWeb or ClientIds.ArtistMobile))
            return;

        await stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.UserId, e.Email, ct);
    }
}
