using Concertable.Auth.Contracts.Events;
using Concertable.B2B.Concert.Contracts.Events;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Contracts.Events;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe<ConcertChangedEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<CredentialRegisteredEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<PayoutOwnerRegisteredEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<PaymentSucceededEvent>(AppHostConstants.ServiceNames.Payment)
            .Subscribe<PaymentFailedEvent>(AppHostConstants.ServiceNames.Payment)
            .Queue<ProcessStripeWebhookCommand>(AppHostConstants.ServiceNames.Payment);
}
