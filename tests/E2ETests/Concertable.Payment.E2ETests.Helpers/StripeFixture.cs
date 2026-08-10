using Concertable.Kernel.ValueObjects;
using Stripe;

namespace Concertable.E2ETests;

public sealed class StripeFixture
{
    private readonly PaymentIntentService paymentIntents;
    private readonly TransferService transfers;
    private readonly PaymentMethodService paymentMethods;
    private readonly RefundService refunds;

    public DateTime LastReset { get; private set; }

    public void Reset() => LastReset = DateTime.UtcNow;

    public StripeFixture(IStripeClient client)
    {
        paymentIntents = new PaymentIntentService(client);
        transfers = new TransferService(client);
        paymentMethods = new PaymentMethodService(client);
        refunds = new RefundService(client);
    }

    public Task<Refund> GetRefundAsync(string refundId, CancellationToken ct = default) =>
        refunds.GetAsync(refundId, cancellationToken: ct);

    public async Task EnsureNoCardsAttachedAsync(string customerId, CancellationToken ct = default)
    {
        var list = await paymentMethods.ListAsync(
            new PaymentMethodListOptions { Customer = customerId, Type = "card", Limit = 100 },
            cancellationToken: ct);

        foreach (var paymentMethod in list.Data.Where(pm => pm.CustomerId == customerId))
            await EnsureDetachedFromCustomerAsync(paymentMethod.Id, customerId, ct);
    }

    private async Task EnsureDetachedFromCustomerAsync(
        string paymentMethodId,
        string customerId,
        CancellationToken ct)
    {
        try
        {
            await paymentMethods.DetachAsync(paymentMethodId, cancellationToken: ct);
        }
        catch (StripeException)
        {
            var current = await paymentMethods.GetAsync(paymentMethodId, cancellationToken: ct);

            if (current.CustomerId == customerId)
                throw;
        }
    }

    public Task AttachTestCardAsync(string customerId, CancellationToken ct = default) =>
        paymentMethods.AttachAsync(
            "pm_card_visa",
            new PaymentMethodAttachOptions { Customer = customerId },
            cancellationToken: ct);

    public Task ConfirmHoldAsync(string clientSecret, string paymentMethodId = "pm_card_visa", CancellationToken ct = default) =>
        paymentIntents.ConfirmAsync(
            clientSecret.Split("_secret_")[0],
            new PaymentIntentConfirmOptions { PaymentMethod = paymentMethodId },
            cancellationToken: ct);

    public async Task<PaymentIntent?> GetCapturedHoldAsync(
        string paymentIntentId,
        decimal amount,
        CancellationToken ct = default)
    {
        var paymentIntent = await paymentIntents.GetAsync(paymentIntentId, cancellationToken: ct);

        return paymentIntent.Amount == Money.Gbp(amount).ToMinorUnits()
            && paymentIntent.Status == "succeeded"
                ? paymentIntent
                : null;
    }

    public async Task<Transfer?> FindTransferAsync(string stripeAccountId, decimal amount)
    {
        var results = await transfers.ListAsync(new TransferListOptions
        {
            Destination = stripeAccountId,
            Created = new DateRangeOptions { GreaterThanOrEqual = LastReset }
        });
        return results.Data.SingleOrDefault(t => t.Amount == Money.Gbp(amount).ToMinorUnits());
    }

}
