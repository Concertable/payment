using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal sealed class SettlementTransactionMapper : ITransactionMapper
{
    public TransactionEntity ToEntity(ITransaction dto)
    {
        var d = (SettlementTransactionDto)dto;
        return SettlementTransactionEntity.Create(d.PayerId, d.PayeeId, d.PaymentIntentId, d.Amount, d.Status, d.BookingId);
    }

    public ITransaction ToDto(TransactionEntity entity)
    {
        var e = (SettlementTransactionEntity)entity;
        return new SettlementTransactionDto
        {
            Id = e.Id,
            BookingId = e.BookingId,
            PayerId = e.PayerId,
            PayeeId = e.PayeeId,
            PaymentIntentId = e.PaymentIntentId,
            Amount = e.Amount,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
