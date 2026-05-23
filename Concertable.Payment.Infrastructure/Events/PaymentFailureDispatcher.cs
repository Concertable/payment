using Concertable.Messaging.Contracts;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal class PaymentFailureDispatcher : IIntegrationEventHandler<PaymentFailedEvent>
{
    private readonly IPaymentFailureHandlerFactory handlerFactory;
    private readonly ILogger<PaymentFailureDispatcher> logger;

    public PaymentFailureDispatcher(
        IPaymentFailureHandlerFactory handlerFactory,
        ILogger<PaymentFailureDispatcher> logger)
    {
        this.handlerFactory = handlerFactory;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentFailedEvent @event, MessageEnvelope envelope, CancellationToken ct)
    {
        var type = @event.Metadata.GetValueOrDefault("type", string.Empty);
        var handler = handlerFactory.Create(type);

        if (handler is null)
        {
            logger.LogWarning(
                "No IPaymentFailureHandler registered for transaction type {TransactionType}; ignoring failure for {TransactionId}",
                type, @event.TransactionId);
            return;
        }

        logger.LogInformation(
            "Dispatching PaymentFailedEvent for transaction {TransactionId} (code {Code}) to handler for type {TransactionType}",
            @event.TransactionId, @event.FailureCode, type);

        await handler.HandleAsync(@event, ct);
    }
}
