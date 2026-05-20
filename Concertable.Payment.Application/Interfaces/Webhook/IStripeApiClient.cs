using Stripe;

namespace Concertable.Payment.Application.Interfaces.Webhook;

public interface IStripeApiClient
{
    Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentCreateOptions options);
    Task<Transfer> CreateTransferAsync(TransferCreateOptions options);
    Task<Refund> CreateRefundAsync(RefundCreateOptions options);
    Task<TransferReversal> CreateTransferReversalAsync(string transferId, TransferReversalCreateOptions options);
}
