using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal sealed class VerifyTransactionMapper : ITransactionMapper
{
    public TransactionEntity ToEntity(ITransaction dto)
    {
        var d = (VerifyTransactionDto)dto;
        return VerifyTransactionEntity.Create(d.PayerId, d.PaymentIntentId, d.ApplicationId);
    }

    public ITransaction ToDto(TransactionEntity entity)
    {
        var e = (VerifyTransactionEntity)entity;
        return new VerifyTransactionDto
        {
            Id = e.Id,
            ApplicationId = e.ApplicationId,
            PayerId = e.PayerId,
            PayeeId = e.PayeeId,
            PaymentIntentId = e.PaymentIntentId,
            Amount = e.Amount,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
