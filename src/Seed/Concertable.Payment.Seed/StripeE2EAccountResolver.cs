using Concertable.Seed.Identity;

namespace Concertable.Payment.Seed;

public sealed class StripeE2EAccountResolver
{
    // Keyed by seed user id — what E2E tests reference. Connect accounts exist for managers only.
    public static readonly Dictionary<Guid, string> AccountIds = new()
    {
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "acct_1TJiMePysoXmht10",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "acct_1TJiMoPupFslP2qz",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "acct_1TJiMjLxk4aCq1Ui",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "acct_1TJiPJLLwGSDilbV",
    };

    // Keyed by seed user id — ticket buyer + managers (managers keep a saved card for holds).
    private static readonly Dictionary<Guid, string> customerIds = new()
    {
        [new Guid("c0000000-0000-0000-0000-000000000001")] = "cus_UIIy9Gbwfr3uAP",
        [new Guid("a1000000-0000-0000-0000-000000000001")] = "cus_UIIy5mCilBtJbR",
        [new Guid("a1000000-0000-0000-0000-000000000002")] = "cus_UIIy5415r69RmJ",
        [new Guid("b1000000-0000-0000-0000-000000000001")] = "cus_UIIymKfHijbNVO",
        [new Guid("b1000000-0000-0000-0000-000000000002")] = "cus_UIJ1qfgxYu624Q",
    };

    /* Provisioning keys payout rows by Payment's opaque owner id, so these views remap the user-keyed tables to
       it: a manager (one with a Connect account) is owned by its tenant; a ticket buyer is owned by itself. */
    private static readonly Dictionary<Guid, string> accountsByOwner =
        AccountIds.ToDictionary(kv => TenantSeedIds.For(kv.Key), kv => kv.Value);
    private static readonly Dictionary<Guid, string> customersByOwner =
        customerIds.ToDictionary(kv => AccountIds.ContainsKey(kv.Key) ? TenantSeedIds.For(kv.Key) : kv.Key, kv => kv.Value);

    // Owner-keyed — used by E2EStripeAccountClient when linking a provisioned account.
    public bool TryGetCustomerId(Guid ownerId, out string id) => customersByOwner.TryGetValue(ownerId, out id!);
    public bool TryGetAccountId(Guid ownerId, out string id) => accountsByOwner.TryGetValue(ownerId, out id!);

    // User-keyed — used by E2E tests/hooks.
    public string ResolveCustomer(Guid userId) =>
        customerIds.TryGetValue(userId, out var id)
            ? id
            : throw new InvalidOperationException($"No E2E Stripe customer ID registered for {userId}.");

    public string ResolveAccount(Guid userId) =>
        AccountIds.TryGetValue(userId, out var id)
            ? id
            : throw new InvalidOperationException($"No E2E Stripe account ID registered for {userId}.");
}
