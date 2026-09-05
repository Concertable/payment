namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionHandler
{
    Task HandleAsync(PaymentSucceededEvent @event, string providerObjectId, CancellationToken ct);
}
