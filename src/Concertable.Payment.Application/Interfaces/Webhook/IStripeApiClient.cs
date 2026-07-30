using Stripe;
using Transfer = Stripe.Transfer;
using Refund = Stripe.Refund;

namespace Concertable.Payment.Application.Interfaces.Webhook;

internal interface IStripeApiClient
{
    Task<PaymentIntent> CreatePaymentIntentAsync(
        PaymentIntentCreateOptions options,
        RequestOptions? requestOptions = null);
    Task<Transfer> CreateTransferAsync(
        TransferCreateOptions options,
        RequestOptions? requestOptions = null);
    Task<Refund> CreateRefundAsync(
        RefundCreateOptions options,
        RequestOptions? requestOptions = null);
    Task<TransferReversal> CreateTransferReversalAsync(
        string transferId,
        TransferReversalCreateOptions options,
        RequestOptions? requestOptions = null);
}
