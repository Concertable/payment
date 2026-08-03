using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;
using Transfer = Stripe.Transfer;
using Refund = Stripe.Refund;

namespace Concertable.Payment.Infrastructure.Services;

internal sealed class StripeApiClient : IStripeApiClient
{
    private readonly PaymentIntentService paymentIntentService;
    private readonly TransferService transferService;
    private readonly RefundService refundService;
    private readonly TransferReversalService transferReversalService;

    public StripeApiClient(
        IOptions<StripeSettings> stripeSettings,
        PaymentIntentService paymentIntentService,
        TransferService transferService,
        RefundService refundService,
        TransferReversalService transferReversalService)
    {
        StripeConfiguration.ApiKey = stripeSettings.Value.SecretKey;
        this.paymentIntentService = paymentIntentService;
        this.transferService = transferService;
        this.refundService = refundService;
        this.transferReversalService = transferReversalService;
    }

    public Task<PaymentIntent> CreatePaymentIntentAsync(
        PaymentIntentCreateOptions options,
        RequestOptions? requestOptions = null,
        CancellationToken ct = default) =>
        paymentIntentService.CreateAsync(options, requestOptions, ct);

    public Task<Transfer> CreateTransferAsync(
        TransferCreateOptions options,
        RequestOptions? requestOptions = null,
        CancellationToken ct = default) =>
        transferService.CreateAsync(options, requestOptions, ct);

    public Task<Refund> CreateRefundAsync(
        RefundCreateOptions options,
        RequestOptions? requestOptions = null,
        CancellationToken ct = default) =>
        refundService.CreateAsync(options, requestOptions, ct);

    public Task<TransferReversal> CreateTransferReversalAsync(
        string transferId,
        TransferReversalCreateOptions options,
        RequestOptions? requestOptions = null,
        CancellationToken ct = default) =>
        transferReversalService.CreateAsync(transferId, options, requestOptions, ct);
}
