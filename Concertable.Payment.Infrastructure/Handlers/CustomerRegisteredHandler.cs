using Concertable.Auth.Contracts;
using Concertable.Auth.Contracts.Events;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Handlers;

internal class CustomerRegisteredHandler(IStripeAccountClient stripeAccountClient)
    : IIntegrationEventHandler<CredentialRegisteredEvent>
{
    public Task HandleAsync(CredentialRegisteredEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        if (e.ClientId is not ClientIds.CustomerWeb and not ClientIds.CustomerMobile)
            return Task.CompletedTask;

        return stripeAccountClient.ProvisionCustomerAsync(e.UserId, e.Email, ct);
    }
}
