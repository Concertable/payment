using Concertable.Kernel.Functional;
using Concertable.Seed.Identity;
using Microsoft.Extensions.Configuration;

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

    private static readonly HashSet<Guid> managerUserIds =
        SeedUsers.Managers.Select(manager => manager.Id).ToHashSet();

    private readonly IReadOnlyDictionary<Guid, string> customersByOwner;
    private readonly HashSet<string> ownedCustomerIds;

    /* Provisioning keys payout rows by Payment's opaque owner id, so these views remap the user-keyed tables to
       it: a manager (one with a Connect account) is owned by its tenant; a ticket buyer is owned by itself. */
    private static readonly Dictionary<Guid, string> accountsByOwner =
        AccountIds.ToDictionary(kv => TenantSeedIds.For(kv.Key), kv => kv.Value);
    public StripeE2EAccountResolver(IConfiguration configuration)
    {
        var customerIds = configuration.GetSection("E2EStripe:Customers")
            .GetChildren()
            .ToDictionary(
                customer => Guid.ParseExact(customer.Key, "N"),
                customer => customer.Value
                    ?? throw new InvalidOperationException($"E2EStripe customer mapping {customer.Key} has no value."));
        if (customerIds.Count == 0)
            throw new InvalidOperationException("E2EStripe customer mappings are not configured.");
        customersByOwner = customerIds.ToDictionary(
            kv => managerUserIds.Contains(kv.Key) ? TenantSeedIds.For(kv.Key) : kv.Key,
            kv => kv.Value);
        ownedCustomerIds = customerIds.Values.ToHashSet(StringComparer.Ordinal);
    }

    public Option<string> ResolveCustomer(Guid ownerId) =>
        customersByOwner.GetValueOrDefault(ownerId).ToOption();

    public Option<string> ResolveAccount(Guid ownerId) =>
        accountsByOwner.GetValueOrDefault(ownerId).ToOption();

    public bool OwnsCustomer(string? customerId) =>
        customerId is not null && ownedCustomerIds.Contains(customerId);
}
