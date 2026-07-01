namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionHandlerFactory
{
    ITransactionHandler Create(string type);
}
