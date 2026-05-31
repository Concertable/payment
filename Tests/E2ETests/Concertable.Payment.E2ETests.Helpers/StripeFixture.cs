using Stripe;

namespace Concertable.E2ETests;

public class StripeFixture
{
    private readonly PaymentIntentService paymentIntents;
    private readonly TransferService transfers;
    private readonly PaymentMethodService paymentMethods;

    public DateTime LastReset { get; private set; }

    public void Reset() => LastReset = DateTime.UtcNow;

    public StripeFixture(StripeClient client)
    {
        paymentIntents = new PaymentIntentService(client);
        transfers = new TransferService(client);
        paymentMethods = new PaymentMethodService(client);
    }

    public async Task DetachAllCardsAsync(string customerId, CancellationToken ct = default)
    {
        var list = await paymentMethods.ListAsync(
            new PaymentMethodListOptions { Customer = customerId, Type = "card", Limit = 100 },
            cancellationToken: ct);

        foreach (var pm in list.Data)
            await paymentMethods.DetachAsync(pm.Id, cancellationToken: ct);
    }

    public Task AttachTestCardAsync(string customerId, CancellationToken ct = default) =>
        paymentMethods.AttachAsync(
            "pm_card_visa",
            new PaymentMethodAttachOptions { Customer = customerId },
            cancellationToken: ct);

    public async Task<PaymentIntent?> FindCapturedHoldAsync(string stripeCustomerId, decimal amount)
    {
        var results = await paymentIntents.ListAsync(new PaymentIntentListOptions
        {
            Customer = stripeCustomerId,
            Created = new DateRangeOptions { GreaterThanOrEqual = LastReset }
        });
        return results.Data.SingleOrDefault(p =>
            p.Amount == (long)(amount * 100) && p.Status == "succeeded");
    }

    public async Task<Transfer?> FindTransferAsync(string stripeAccountId, decimal amount)
    {
        var results = await transfers.ListAsync(new TransferListOptions
        {
            Destination = stripeAccountId,
            Created = new DateRangeOptions { GreaterThanOrEqual = LastReset }
        });
        return results.Data.SingleOrDefault(t => t.Amount == (long)(amount * 100));
    }

}
