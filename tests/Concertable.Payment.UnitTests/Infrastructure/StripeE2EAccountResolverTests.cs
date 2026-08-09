using Concertable.Payment.Infrastructure.Services.Webhook;
using Concertable.Payment.Seed;
using Concertable.Seed.Identity;
using Microsoft.Extensions.Configuration;
using Stripe;

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

    [Fact]
    public void ResolveCustomer_UsesRunConfiguration()
    {
        var customerId = this.resolver.ResolveCustomer(VenueManagerId);

        Assert.Equal(OwnedCustomerId, customerId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void ShouldProcess_AcceptsOnlyIntentEventsOwnedByTheRun(
        bool isPaymentIntent,
        bool isOwned)
    {
        var customerId = isOwned ? OwnedCustomerId : "cus_another_run";
        var stripeEvent = new Event
        {
            Data = new EventData
            {
                Object = isPaymentIntent
                    ? new PaymentIntent { CustomerId = customerId }
                    : new SetupIntent { CustomerId = customerId },
            },
        };

        var shouldProcess = ((IStripeEventFilter)this.resolver).ShouldProcess(stripeEvent);

        Assert.Equal(isOwned, shouldProcess);
    }
}
