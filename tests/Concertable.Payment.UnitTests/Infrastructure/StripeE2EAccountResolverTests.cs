using Concertable.Kernel.Functional;
using Concertable.Payment.Seed;
using Concertable.Seed.Identity;
using Microsoft.Extensions.Configuration;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeE2EAccountResolverTests
{
    private const string OwnedCustomerId = "cus_owned";
    private static readonly Guid VenueManagerId = SeedUsers.VenueManagerId(1);
    private readonly StripeE2EAccountResolver resolver;

    public StripeE2EAccountResolverTests()
    {
        var values = SeedUsers.Managers.ToDictionary(
            manager => $"E2EStripe:Customers:{manager.Id:N}",
            manager => (string?)$"cus_{manager.Id:N}");
        values[$"E2EStripe:Customers:{VenueManagerId:N}"] = OwnedCustomerId;
        values[$"E2EStripe:Customers:{SeedCustomers.CustomerId(1):N}"] = "cus_customer_1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        this.resolver = new StripeE2EAccountResolver(configuration);
    }

    #region ResolveCustomer

    [Fact]
    public void ResolveCustomer_UsesRunConfiguration()
    {
        var customerId = this.resolver.ResolveCustomer(TenantSeedIds.For(VenueManagerId));

        Assert.Equal(Option.Some(OwnedCustomerId), customerId);
    }

    [Fact]
    public void ResolveCustomer_ReturnsNoneForUnmappedOwner()
    {
        var customerId = this.resolver.ResolveCustomer(Guid.NewGuid());

        Assert.Equal(Option.None<string>(), customerId);
    }

    #endregion

    #region ResolveAccount

    [Fact]
    public void ResolveAccount_UsesConfiguredAccountMapping()
    {
        var accountId = this.resolver.ResolveAccount(TenantSeedIds.For(VenueManagerId));

        Assert.Equal(Option.Some(StripeE2EAccountResolver.AccountIds[VenueManagerId]), accountId);
    }

    #endregion

    #region OwnsCustomer

    [Theory]
    [InlineData(OwnedCustomerId, true)]
    [InlineData("cus_another_run", false)]
    [InlineData(null, false)]
    public void OwnsCustomer_ReturnsWhetherCustomerBelongsToRun(string? customerId, bool expected)
    {
        var ownsCustomer = this.resolver.OwnsCustomer(customerId);

        Assert.Equal(expected, ownsCustomer);
    }

    #endregion
}
