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
            .ForService(PaymentConstants.ServiceName)
            .Subscribe<ConcertChangedEvent>()
            .Subscribe<CredentialRegisteredEvent>()
            .Subscribe<PayoutOwnerRegisteredEvent>()
            .Subscribe<PaymentSucceededEvent>()
            .Subscribe<PaymentFailedEvent>()
            .Queue<CaptureEscrowCommand>()
            .Queue<DepositEscrowCommand>()
            .Queue<RefundEscrowCommand>()
            .Queue<ProcessStripeWebhookCommand>();
}
