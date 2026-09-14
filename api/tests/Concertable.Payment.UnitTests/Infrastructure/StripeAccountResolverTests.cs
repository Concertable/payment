using Concertable.Kernel.Functional;
using Concertable.Payment.E2ETests.Stripe;
using Microsoft.Extensions.Configuration;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeAccountResolverTests
{
    private const string OwnedCustomerId = "cus_owned";
    private static readonly Guid OwnerId = StripeAccountResolver.AccountIds.Keys.First();
    private readonly StripeAccountResolver resolver;

    public StripeAccountResolverTests()
    {
        var values = new Dictionary<string, string?>
        {
            [$"E2EStripe:Customers:{OwnerId:N}"] = OwnedCustomerId,
            [$"E2EStripe:Customers:{Guid.CreateVersion7():N}"] = "cus_other"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        this.resolver = new StripeAccountResolver(configuration);
    }

    #region ResolveCustomer

    [Fact]
    public void ResolveCustomer_UsesRunConfiguration()
    {
        var customerId = this.resolver.ResolveCustomer(OwnerId);

        Assert.Equal(Option.Some(OwnedCustomerId), customerId);
    }

    [Fact]
    public void ResolveCustomer_ReturnsNoneForUnmappedOwner()
    {
        var customerId = this.resolver.ResolveCustomer(Guid.Empty);

        Assert.Equal(Option.None<string>(), customerId);
    }

    #endregion

    #region ResolveAccount

    [Fact]
    public void ResolveAccount_UsesConfiguredAccountMapping()
    {
        var accountId = this.resolver.ResolveAccount(OwnerId);

        Assert.Equal(Option.Some(StripeAccountResolver.AccountIds[OwnerId]), accountId);
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
