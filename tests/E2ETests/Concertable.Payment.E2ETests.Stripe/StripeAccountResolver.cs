using Concertable.Kernel.Functional;
using Concertable.Payment.TestKit;
using Concertable.Seed.Identity;
using Microsoft.Extensions.Configuration;

namespace Concertable.Payment.E2ETests.Stripe;

public sealed class StripeAccountResolver
{
    // Keyed by seed user id — what E2E tests reference. Connect accounts exist for managers only.
    public static IReadOnlyDictionary<Guid, string> AccountIds => StripeTestAccounts.BySeedUserId;

    private static readonly HashSet<Guid> managerUserIds =
        SeedUsers.Managers.Select(manager => manager.Id).ToHashSet();

    private readonly IReadOnlyDictionary<Guid, string> customersByOwner;
    private readonly HashSet<string> ownedCustomerIds;

    /* Provisioning keys payout rows by Payment's opaque owner id, so these views remap the user-keyed tables to
       it: a manager (one with a Connect account) is owned by its tenant; a ticket buyer is owned by itself. */
    private static readonly Dictionary<Guid, string> accountsByOwner =
        AccountIds.ToDictionary(kv => TenantSeedIds.For(kv.Key), kv => kv.Value);
    public StripeAccountResolver(IConfiguration configuration)
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
