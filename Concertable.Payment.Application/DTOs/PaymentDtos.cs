using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.DTOs;

internal sealed record PaymentMethodDto(string Brand, string Last4, int ExpMonth, int ExpYear);

internal sealed record PaymentDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public required string PaymentMethodId { get; set; }
    public required string Description { get; set; }
    public int ConcertId { get; set; }
    public Guid UserId { get; set; }
}

internal sealed record TicketTransactionDto : ITransaction
{
    public int Id { get; set; }
    public TransactionType TransactionType => TransactionType.Ticket;
    public int ConcertId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public required string PaymentIntentId { get; set; }
    public long Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

internal sealed record SettlementTransactionDto : ITransaction
{
    public int Id { get; set; }
    public TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public required string PaymentIntentId { get; set; }
    public long Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

internal sealed record VerifyTransactionDto : ITransaction
{
    public int Id { get; set; }
    public TransactionType TransactionType => TransactionType.Verify;
    public int ApplicationId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public required string PaymentIntentId { get; set; }
    public long Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
