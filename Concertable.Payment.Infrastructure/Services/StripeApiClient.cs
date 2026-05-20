using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Stripe;

namespace Concertable.Payment.Infrastructure.Services;

internal class StripeApiClient : IStripeApiClient
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

    public Task<PaymentIntent> CreatePaymentIntentAsync(PaymentIntentCreateOptions options) =>
        paymentIntentService.CreateAsync(options);

    public Task<Transfer> CreateTransferAsync(TransferCreateOptions options) =>
        transferService.CreateAsync(options);

    public Task<Refund> CreateRefundAsync(RefundCreateOptions options) =>
        refundService.CreateAsync(options);

    public Task<TransferReversal> CreateTransferReversalAsync(string transferId, TransferReversalCreateOptions options) =>
        transferReversalService.CreateAsync(transferId, options);
}
