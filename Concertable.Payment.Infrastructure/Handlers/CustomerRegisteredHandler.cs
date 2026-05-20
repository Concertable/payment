using Concertable.User.Contracts.Events;
using Concertable.Payment.Application.Interfaces;
using Concertable.Shared;

namespace Concertable.Payment.Infrastructure.Handlers;

internal class CustomerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    : IIntegrationEventHandler<CustomerRegisteredEvent>
{
    public Task HandleAsync(CustomerRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default) =>
        stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
}
