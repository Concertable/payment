namespace Concertable.Payment.Seed;

public sealed class StripeE2EAccountResolver
{
    private static readonly Dictionary<Guid, string> customerIds = new()
    {
        [new Guid("c0000000-0000-0000-0000-000000000001")] = "cus_UIIy9Gbwfr3uAP",
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "cus_UIIy5mCilBtJbR",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "cus_UIIy5415r69RmJ",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "cus_UIIymKfHijbNVO",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "cus_UIJ1qfgxYu624Q",
    };

    public static readonly Dictionary<Guid, string> AccountIds = new()
    {
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "acct_1TJiMePysoXmht10",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "acct_1TJiMoPupFslP2qz",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "acct_1TJiMjLxk4aCq1Ui",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "acct_1TJiPJLLwGSDilbV",
    };

    public bool TryGetCustomerId(Guid userId, out string id) => customerIds.TryGetValue(userId, out id!);
    public bool TryGetAccountId(Guid userId, out string id) => AccountIds.TryGetValue(userId, out id!);

    public string ResolveCustomer(Guid userId) =>
        customerIds.TryGetValue(userId, out var id)
            ? id
            : throw new InvalidOperationException($"No E2E Stripe customer ID registered for {userId}.");

    public string ResolveAccount(Guid userId) =>
        AccountIds.TryGetValue(userId, out var id)
            ? id
            : throw new InvalidOperationException($"No E2E Stripe account ID registered for {userId}.");
}
