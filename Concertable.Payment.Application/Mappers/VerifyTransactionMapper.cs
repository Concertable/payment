using Concertable.Payment.Application.DTOs;
using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.Mappers;

internal class VerifyTransactionMapper : ITransactionMapper
{
    public TransactionEntity ToEntity(ITransaction dto)
    {
        var d = (VerifyTransactionDto)dto;
        return VerifyTransactionEntity.Create(d.FromUserId, d.PaymentIntentId, d.ApplicationId);
    }

    public ITransaction ToDto(TransactionEntity entity)
    {
        var e = (VerifyTransactionEntity)entity;
        return new VerifyTransactionDto
        {
            Id = e.Id,
            ApplicationId = e.ApplicationId,
            FromUserId = e.FromUserId,
            ToUserId = e.ToUserId,
            PaymentIntentId = e.PaymentIntentId,
            Amount = e.Amount,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
