using Concertable.Auth.Contracts.Events;
using Concertable.B2B.Concert.Contracts.Events;
using Concertable.B2B.Tenant.Contracts.Events;
using Concertable.Payment.Contracts.Events;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe<ConcertChangedEvent>("payment-concert-changed",       "concertable-payment")
            .Subscribe<CredentialRegisteredEvent>("payment-credential-registered", "concertable-payment")
            .Subscribe<TenantCreatedEvent>("payment-tenant-created",        "concertable-payment")
            .Subscribe<PaymentSucceededEvent>("payment-payment-succeeded",     "concertable-payment")
            .Subscribe<PaymentFailedEvent>("payment-payment-failed",        "concertable-payment")
            // Both names exist across the service-scoped queue rename: Payment keeps using the
            // unscoped name until platform-sync pins it to the Messaging version that scopes it.
            .Queue("command-processstripewebhookcommand")
            .Queue("command-concertable-payment-processstripewebhookcommand");
}
