using Concertable.B2B.Tenant.Contracts.Events;
using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Infrastructure.Handlers;

/// <summary>
/// Provisions the Stripe customer + Express Connect account for a B2B operator/artist when their tenant is
/// created. Payment stays tenancy-agnostic: the tenant id is consumed purely as the opaque payout-account
/// owner key (<see cref="PayoutAccountEntity.OwnerId"/>). Replaces the per-user <c>ManagerRegisteredHandler</c>;
/// customers are still provisioned per-user by <c>CustomerRegisteredHandler</c>.
/// </summary>
internal sealed class TenantCreatedHandler : IIntegrationEventHandler<TenantCreatedEvent>
{
    private readonly IStripeAccountClient stripeAccountClient;

    public TenantCreatedHandler(IStripeAccountClient stripeAccountClient)
    {
        this.stripeAccountClient = stripeAccountClient;
    }

    public async Task HandleAsync(TenantCreatedEvent e, MessageEnvelope envelope, CancellationToken ct = default)
    {
        await stripeAccountClient.ProvisionCustomerAsync(e.TenantId, e.Email, ct);
        await stripeAccountClient.ProvisionConnectAccountAsync(e.TenantId, e.Email, ct);
    }
}
