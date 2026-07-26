using Concertable.Auth.Contracts.Events;
using Concertable.B2B.Concert.Contracts.Events;
using Concertable.B2B.Tenant.Contracts.Events;
using Concertable.Payment.Contracts.Events;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe<ConcertChangedEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<CredentialRegisteredEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<TenantCreatedEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<PaymentSucceededEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<PaymentFailedEvent>(AppHostConstants.ServiceNames.Payment)
            .Queue("command-processstripewebhookcommand")
            .Queue("command-concertable-payment-processstripewebhookcommand");
}
