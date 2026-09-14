using Concertable.Messaging.Contracts;
using Concertable.Payment.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Concertable.Payment.Infrastructure.Events;

internal sealed class PaymentTransactionHandler : IIntegrationEventHandler<PaymentSucceededEvent>
{
    private readonly ITransactionHandlerFactory handlerFactory;
    private readonly IPaymentOperationResolver paymentOperationResolver;
    private readonly ILogger<PaymentTransactionHandler> logger;

    public PaymentTransactionHandler(
        ITransactionHandlerFactory handlerFactory,
        IPaymentOperationResolver paymentOperationResolver,
        ILogger<PaymentTransactionHandler> logger)
    {
        this.handlerFactory = handlerFactory;
        this.paymentOperationResolver = paymentOperationResolver;
        this.logger = logger;
    }

    public async Task HandleAsync(PaymentSucceededEvent @event, MessageEnvelope envelope, CancellationToken ct)
    {
        var type = @event.Metadata.GetValue(PaymentMetadataKeys.Type);
        var providerObjectId = await paymentOperationResolver.ResolveProviderObjectIdAsync(
            @event.Reference,
            ct);
        logger.DispatchingPaymentSucceededEvent(@event.Reference.ClientReference, type);

        var handler = handlerFactory.Create(type);
        await handler.HandleAsync(@event, providerObjectId, ct);
    }
}
