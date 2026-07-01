namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionHandler
{
    Task HandleAsync(PaymentSucceededEvent @event, CancellationToken ct);
}
