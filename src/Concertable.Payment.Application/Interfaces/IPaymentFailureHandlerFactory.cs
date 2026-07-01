namespace Concertable.Payment.Application.Interfaces;

internal interface IPaymentFailureHandlerFactory
{
    IPaymentFailureHandler? Create(string type);
}
