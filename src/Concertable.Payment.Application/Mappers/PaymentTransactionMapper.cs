using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal sealed class PaymentTransactionMapper : ITransactionMapper
{
    public TransactionEntity ToEntity(ITransaction dto)
    {
        var d = (PaymentTransactionDto)dto;
        return PaymentTransactionEntity.Create(
            d.PayerId,
            d.PayeeId,
            d.PaymentIntentId,
            d.Amount,
            d.Status,
            new(d.OperationType, d.ClientReference));
    }

    public ITransaction ToDto(TransactionEntity entity)
    {
        var e = (PaymentTransactionEntity)entity;
        return new PaymentTransactionDto
        {
            Id = e.Id,
            OperationType = e.OperationType,
            ClientReference = e.ClientReference,
            PayerId = e.PayerId,
            PayeeId = e.PayeeId,
            PaymentIntentId = e.PaymentIntentId,
            Amount = e.Amount,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
