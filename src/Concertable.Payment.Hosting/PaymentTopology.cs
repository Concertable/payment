using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Hosting;

public static class PaymentTopology
{
    extension(AsbTopology topology)
    {
        public AsbTopology AddPaymentTopology() => topology
            .Publish<PaymentSucceededEvent>()
            .Publish<PaymentFailedEvent>()
            .Publish<CaptureEscrowSucceededEvent>()
            .Publish<CaptureEscrowRejectedEvent>()
            .Publish<DepositEscrowSucceededEvent>()
            .Publish<DepositEscrowRejectedEvent>()
            .Publish<RefundEscrowSucceededEvent>()
            .Publish<RefundEscrowRejectedEvent>()
            .Publish<RefundEscrowDeferredEvent>()
            .WithService(PaymentConstants.ServiceName)
            .Subscribe<PaymentMethodOwnerRegisteredEvent>()
            .Subscribe<PayoutOwnerRegisteredEvent>()
            .Subscribe<PaymentSucceededEvent>()
            .Subscribe<PaymentFailedEvent>()
            .Queue<CaptureEscrowCommand>()
            .Queue<DepositEscrowCommand>()
            .Queue<RefundEscrowCommand>()
            .Queue<ProcessStripeWebhookCommand>();
    }
}
