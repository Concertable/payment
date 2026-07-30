using Concertable.Messaging.Contracts;
using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Contracts.Events;
using Concertable.Payment.Infrastructure.Handlers;
using Moq;

namespace Concertable.Payment.UnitTests.Infrastructure.Handlers;

public sealed class PayoutOwnerRegisteredHandlerTests
{
    private readonly Mock<IStripeAccountClient> stripeAccountClient = new();

    [Fact]
    public async Task HandleAsync_ProvisionsCustomerAndConnectAccountForOwner()
    {
        var sut = new PayoutOwnerRegisteredHandler(stripeAccountClient.Object);
        var ownerId = Guid.NewGuid();
        var evt = new PayoutOwnerRegisteredEvent(ownerId, "owner@test.com");
        var envelope = new MessageEnvelope(Guid.NewGuid(), MessageTypeAttribute.Resolve(typeof(PayoutOwnerRegisteredEvent)), DateTimeOffset.UtcNow);

        await sut.HandleAsync(evt, envelope);

        stripeAccountClient.Verify(c => c.ProvisionCustomerAsync(ownerId, "owner@test.com", It.IsAny<CancellationToken>()), Times.Once);
        stripeAccountClient.Verify(c => c.ProvisionConnectAccountAsync(ownerId, "owner@test.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}
