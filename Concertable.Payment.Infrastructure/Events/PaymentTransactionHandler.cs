using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class PaymentTransactionHandler : IIntegrationEventHandler<PaymentSucceededEvent>
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
        logger.LogInformation(
            "Dispatching PaymentSucceededEvent for transaction {TransactionId} to handler for type {TransactionType}",
            @event.TransactionId, type);

        var handler = handlerFactory.Create(type);
        await handler.HandleAsync(@event, ct);
    }
}
