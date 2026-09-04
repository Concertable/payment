using Concertable.Kernel.Functional;
using Concertable.Payment.TestKit;
using Microsoft.Extensions.Configuration;

namespace Concertable.Payment.E2ETests.Stripe;

public sealed class StripeAccountResolver
{
    public static IReadOnlyDictionary<Guid, string> AccountIds => StripeTestAccounts.ByOwnerId;

    private readonly IReadOnlyDictionary<Guid, string> customersByOwner;
    private readonly HashSet<string> ownedCustomerIds;

    public StripeAccountResolver(IConfiguration configuration)
    {
        customersByOwner = configuration.GetSection("E2EStripe:Customers")
            .GetChildren()
            .ToDictionary(
                customer => Guid.ParseExact(customer.Key, "N"),
                customer => customer.Value
                    ?? throw new InvalidOperationException($"E2EStripe customer mapping {customer.Key} has no value."));
        if (customersByOwner.Count == 0)
            throw new InvalidOperationException("E2EStripe customer mappings are not configured.");
        ownedCustomerIds = customersByOwner.Values.ToHashSet(StringComparer.Ordinal);
    }

    public Option<string> ResolveCustomer(Guid ownerId) =>
        customersByOwner.GetValueOrDefault(ownerId).ToOption();

    public Option<string> ResolveAccount(Guid ownerId) =>
        AccountIds.GetValueOrDefault(ownerId).ToOption();

    public bool OwnsCustomer(string? customerId) =>
        customerId is not null && ownedCustomerIds.Contains(customerId);
}
