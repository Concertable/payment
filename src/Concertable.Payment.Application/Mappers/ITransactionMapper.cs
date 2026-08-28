using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal interface ITransactionMapper
{
    TransactionEntity ToEntity(ITransaction dto);
    ITransaction ToDto(TransactionEntity entity);
}
