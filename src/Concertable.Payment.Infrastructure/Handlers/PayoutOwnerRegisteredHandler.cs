using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class PayoutOwnerRegisteredHandler : IIntegrationEventHandler<PayoutOwnerRegisteredEvent>
{
    private readonly IStripeAccountClient stripeAccountClient;

    public PayoutOwnerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    {
        this.stripeAccountClient = stripeAccountClient;
    }

    public async Task HandleAsync(PayoutOwnerRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        await stripeAccountClient.ProvisionCustomerAsync(e.OwnerId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.OwnerId, e.Email, ct);
    }
}
