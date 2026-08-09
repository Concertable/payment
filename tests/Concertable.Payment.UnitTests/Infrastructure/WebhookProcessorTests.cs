using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Infrastructure;
using Concertable.Payment.Infrastructure.Services.Webhook;
using Microsoft.Extensions.Logging;
using Moq;
using Stripe;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class WebhookProcessorTests
{
    private readonly Mock<IStripeEventRepository> stripeEventRepository;
    private readonly Mock<IOutboxUnitOfWorkBehavior> outboxBehavior;
    private readonly Mock<IStripeWebhookHandler<PaymentIntent>> paymentIntentHandler;
    private readonly Mock<IStripeWebhookHandler<SetupIntent>> setupIntentHandler;
    private readonly Mock<IStripeEventFilter> eventFilter;
    private readonly WebhookProcessor processor;

    public WebhookProcessorTests()
    {
        this.stripeEventRepository = new Mock<IStripeEventRepository>();
        this.outboxBehavior = new Mock<IOutboxUnitOfWorkBehavior>();
        this.paymentIntentHandler = new Mock<IStripeWebhookHandler<PaymentIntent>>();
        this.setupIntentHandler = new Mock<IStripeWebhookHandler<SetupIntent>>();
        this.eventFilter = new Mock<IStripeEventFilter>();
        this.processor = new WebhookProcessor(
            this.stripeEventRepository.Object,
            this.outboxBehavior.Object,
            TimeProvider.System,
            this.paymentIntentHandler.Object,
            this.setupIntentHandler.Object,
            this.eventFilter.Object,
            Mock.Of<ILogger<WebhookProcessor>>());
    }

    [Fact]
    public async Task ProcessAsync_SkipsForeignEventBeforeDeduplicationAndHandling()
    {
        var stripeEvent = new Event
        {
            Id = "evt_foreign",
            Data = new EventData
            {
                Object = new PaymentIntent { CustomerId = "cus_foreign" },
            },
        };
        this.eventFilter
            .Setup(filter => filter.ShouldProcess(stripeEvent))
            .Returns(false);

        await this.processor.ProcessAsync(stripeEvent, CancellationToken.None);

        this.stripeEventRepository.Verify(
            repository => repository.EventExistsAsync(It.IsAny<string>()),
            Times.Never);
        this.outboxBehavior.Verify(
            behavior => behavior.ExecuteAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.paymentIntentHandler.Verify(
            handler => handler.HandleAsync(It.IsAny<Event>(), It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.setupIntentHandler.Verify(
            handler => handler.HandleAsync(It.IsAny<Event>(), It.IsAny<SetupIntent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
