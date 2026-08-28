using Concertable.Auth.Contracts.Events;
using Concertable.B2B.Concert.Contracts.Events;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Hosting;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology) =>
        topology
            .Publish<PaymentSucceededEvent>()
            .Publish<PaymentFailedEvent>()
            .Publish<CaptureEscrowSucceededEvent>()
            .Publish<CaptureEscrowRejectedEvent>()
            .Publish<DepositEscrowSucceededEvent>()
            .Publish<DepositEscrowRejectedEvent>()
            .Publish<RefundEscrowSucceededEvent>()
            .Publish<RefundEscrowRejectedEvent>()
            .Publish<RefundEscrowDeferredEvent>()
            .Subscribe<ConcertChangedEvent>(PaymentConstants.ServiceName)
            .Subscribe<CredentialRegisteredEvent>(PaymentConstants.ServiceName)
            .Subscribe<PayoutOwnerRegisteredEvent>(PaymentConstants.ServiceName)
            .Subscribe<PaymentSucceededEvent>(PaymentConstants.ServiceName)
            .Subscribe<PaymentFailedEvent>(PaymentConstants.ServiceName)
            .Queue<CaptureEscrowCommand>(PaymentConstants.ServiceName)
            .Queue<DepositEscrowCommand>(PaymentConstants.ServiceName)
            .Queue<RefundEscrowCommand>(PaymentConstants.ServiceName)
            .Queue<ProcessStripeWebhookCommand>(PaymentConstants.ServiceName);
}
