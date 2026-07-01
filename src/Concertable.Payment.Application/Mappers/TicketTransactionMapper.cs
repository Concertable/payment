using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal sealed class TicketTransactionMapper : ITransactionMapper
{
    public TransactionEntity ToEntity(ITransaction dto)
    {
        var d = (TicketTransactionDto)dto;
        return TicketTransactionEntity.Create(d.PayerId, d.PayeeId, d.PaymentIntentId, d.Amount, d.Status, d.ConcertId);
    }

    public ITransaction ToDto(TransactionEntity entity)
    {
        var e = (TicketTransactionEntity)entity;
        return new TicketTransactionDto
        {
            Id = e.Id,
            ConcertId = e.ConcertId,
            PayerId = e.PayerId,
            PayeeId = e.PayeeId,
            PaymentIntentId = e.PaymentIntentId,
            Amount = e.Amount,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
