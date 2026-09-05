using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class PaymentFailureDispatcher : IIntegrationEventHandler<PaymentFailedEvent>
{
    private readonly IPaymentFailureHandlerFactory handlerFactory;
    private readonly IPaymentOperationResolver paymentOperationResolver;
    private readonly ILogger<PaymentFailureDispatcher> logger;

    public PaymentFailureDispatcher(
        IPaymentFailureHandlerFactory handlerFactory,
        IPaymentOperationResolver paymentOperationResolver,
        ILogger<PaymentFailureDispatcher> logger)
    {
        this.handlerFactory = handlerFactory;
        this.paymentOperationResolver = paymentOperationResolver;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentFailedEvent @event, MessageEnvelope envelope, CancellationToken ct)
    {
        var type = @event.Metadata.GetValue(PaymentMetadataKeys.Type);
        var handler = handlerFactory.Create(type);

        if (handler is null)
        {
            logger.NoPaymentFailureHandlerRegistered(type, @event.Reference.ClientReference);
            return;
        }

        var providerObjectId = await paymentOperationResolver.ResolveProviderObjectIdAsync(
            @event.Reference,
            ct);
        logger.DispatchingPaymentFailedEvent(@event.Reference.ClientReference, @event.FailureCode, type);

        await handler.HandleAsync(@event, providerObjectId, ct);
    }
}
