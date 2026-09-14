using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Infrastructure.Handlers;

internal sealed class PaymentMethodOwnerRegisteredHandler
    : IIntegrationEventHandler<PaymentMethodOwnerRegisteredEvent>
{
    private readonly IStripeAccountClient stripeAccountClient;

    public PaymentMethodOwnerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    {
        this.stripeAccountClient = stripeAccountClient;
    }

    public Task HandleAsync(
        PaymentMethodOwnerRegisteredEvent e,
        MessageEnvelope envelope,
        CancellationToken ct = default) =>
        stripeAccountClient.ProvisionCustomerAsync(e.OwnerId, e.Email, ct);
}
