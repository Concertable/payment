namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentFailureHandler
{
    Task HandleAsync(PaymentFailedEvent @event, CancellationToken ct);
}
