using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Infrastructure.Handlers;

/// <summary>
/// Provisions the Stripe customer + Express Connect account for a payout owner (B2B operator/artist) when
/// their tenant is created. Payment stays tenancy-agnostic: <see cref="PayoutOwnerRegisteredEvent.OwnerId"/>
/// is consumed purely as the opaque payout-account owner key (<see cref="PayoutAccountEntity.OwnerId"/>).
/// Replaces the per-user <c>ManagerRegisteredHandler</c>; customers are still provisioned per-user by
/// <c>CustomerRegisteredHandler</c>.
/// </summary>
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
