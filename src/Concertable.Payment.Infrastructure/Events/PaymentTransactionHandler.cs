using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class PaymentTransactionHandler : IIntegrationEventHandler<PaymentSucceededEvent>
{
    private readonly ITransactionHandlerFactory handlerFactory;
    private readonly ILogger<PaymentTransactionHandler> logger;

    public PaymentTransactionHandler(
        ITransactionHandlerFactory handlerFactory,
        ILogger<PaymentTransactionHandler> logger)
    {
        this.handlerFactory = handlerFactory;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent @event, MessageEnvelope envelope, CancellationToken ct)
    {
        var type = @event.Metadata.GetValueOrDefault("type", string.Empty);
        logger.DispatchingPaymentSucceededEvent(@event.TransactionId, type);

        var handler = handlerFactory.Create(type);
        await handler.HandleAsync(@event, ct);
    }
}
