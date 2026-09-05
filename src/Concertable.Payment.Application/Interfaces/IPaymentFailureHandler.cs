namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentFailureHandler
{
    Task HandleAsync(PaymentFailedEvent @event, string providerObjectId, CancellationToken ct);
}
