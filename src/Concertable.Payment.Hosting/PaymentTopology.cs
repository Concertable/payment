using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Events;

namespace Concertable.Payment.Hosting;

public static class PaymentTopology
{
    public static AsbTopology AddPaymentTopology(this AsbTopology topology)
    {
        topology.WithService(PaymentConstants.ServiceName)
                .Publish<PaymentSucceededEvent>()
                .Publish<PaymentFailedEvent>()
                .Publish<CaptureEscrowSucceededEvent>()
                .Publish<CaptureEscrowRejectedEvent>()
                .Publish<DepositEscrowSucceededEvent>()
                .Publish<DepositEscrowRejectedEvent>()
                .Publish<RefundEscrowSucceededEvent>()
                .Publish<RefundEscrowRejectedEvent>()
                .Publish<RefundEscrowDeferredEvent>()
                .Subscribe<PaymentMethodOwnerRegisteredEvent>()
                .Subscribe<PayoutOwnerRegisteredEvent>()
                .Subscribe<PaymentSucceededEvent>()
                .Subscribe<PaymentFailedEvent>()
                .Queue<CaptureEscrowCommand>()
                .Queue<DepositEscrowCommand>()
                .Queue<RefundEscrowCommand>()
                .Queue<ProcessStripeWebhookCommand>();

        return topology;
    }
}
