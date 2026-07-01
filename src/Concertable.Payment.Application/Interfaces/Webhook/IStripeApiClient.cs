using Stripe;
using Transfer = Stripe.Transfer;
using Refund = Stripe.Refund;

namespace Concertable.Payment.Application.Interfaces.Webhook;

internal interface IStripeApiClient
{
    Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentCreateOptions options);
    Task<Transfer> CreateTransferAsync(TransferCreateOptions options);
    Task<Refund> CreateRefundAsync(RefundCreateOptions options);
    Task<TransferReversal> CreateTransferReversalAsync(string transferId, TransferReversalCreateOptions options);
}
