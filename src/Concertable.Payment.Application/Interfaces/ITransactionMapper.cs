namespace Concertable.Payment.Application.Interfaces;

internal interface ITransactionMapper
{
    TransactionEntity ToEntity(ITransaction dto);
    ITransaction ToDto(TransactionEntity entity);
}
