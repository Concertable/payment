using Concertable.Auth.Contracts.Events;
using Concertable.B2B.Concert.Contracts.Events;
using Concertable.Payment.Application.Commands;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Hosting;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Subscribe<ConcertChangedEvent>(PaymentConstants.ServiceName)
            .Subscribe<CredentialRegisteredEvent>(PaymentConstants.ServiceName)
            .Subscribe<PayoutOwnerRegisteredEvent>(PaymentConstants.ServiceName)
            .Subscribe<PaymentSucceededEvent>(PaymentConstants.ServiceName)
            .Subscribe<PaymentFailedEvent>(PaymentConstants.ServiceName)
            .Queue<ProcessStripeWebhookCommand>(PaymentConstants.ServiceName);
}
