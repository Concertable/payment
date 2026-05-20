using System.Collections.Frozen;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal class TransactionMapper : ITransactionMapper
{
    private static readonly FrozenDictionary<TransactionType, ITransactionMapper> mappers =
        new Dictionary<TransactionType, ITransactionMapper>
        {
            [TransactionType.Ticket] = new TicketTransactionMapper(),
            [TransactionType.Settlement] = new SettlementTransactionMapper(),
            [TransactionType.Verify] = new VerifyTransactionMapper(),
        }.ToFrozenDictionary();

    public TransactionEntity ToEntity(ITransaction dto) => mappers[dto.TransactionType].ToEntity(dto);
    public ITransaction ToDto(TransactionEntity entity) => mappers[entity.TransactionType].ToDto(entity);
}
