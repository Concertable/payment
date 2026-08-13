using Concertable.Payment.Application.Interfaces.Webhook;
using Concertable.Payment.E2ETests.Stripe;
using Concertable.Seed.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class StripeWebhookProcessorTests
{
    private const string OwnedCustomerId = "cus_owned";
    private readonly Mock<IWebhookProcessor> inner;
    private readonly StripeWebhookProcessor processor;

    public StripeWebhookProcessorTests()
    {
        var values = SeedUsers.Managers.ToDictionary(
            manager => $"E2EStripe:Customers:{manager.Id:N}",
            manager => (string?)$"cus_{manager.Id:N}");
        values[$"E2EStripe:Customers:{SeedUsers.VenueManagerId(1):N}"] = OwnedCustomerId;
        values[$"E2EStripe:Customers:{SeedCustomers.CustomerId(1):N}"] = "cus_customer_1";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        this.inner = new Mock<IWebhookProcessor>();
        this.processor = new StripeWebhookProcessor(
            this.inner.Object,
            new StripeAccountResolver(configuration),
            NullLogger<StripeWebhookProcessor>.Instance);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_ForeignIntent_DoesNotDelegate(bool isPaymentIntent)
    {
        var stripeEvent = CreateEvent(isPaymentIntent, "cus_another_run");

        await this.processor.ProcessAsync(stripeEvent, CancellationToken.None);

        this.inner.Verify(
            processor => processor.ProcessAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_OwnedIntent_Delegates(bool isPaymentIntent)
    {
        var stripeEvent = CreateEvent(isPaymentIntent, OwnedCustomerId);

        await this.processor.ProcessAsync(stripeEvent, CancellationToken.None);

        this.inner.Verify(
            processor => processor.ProcessAsync(stripeEvent, CancellationToken.None),
            Times.Once);
    }

    private static Event CreateEvent(bool isPaymentIntent, string customerId) =>
        new()
        {
            Id = "evt_test",
            Data = new EventData
            {
                Object = isPaymentIntent
                    ? new PaymentIntent { CustomerId = customerId }
                    : new SetupIntent { CustomerId = customerId },
            },
        };
}
