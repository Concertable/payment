using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.DTOs;

internal sealed record PaymentMethodDto(string Brand, string Last4, int ExpMonth, int ExpYear);

internal sealed record PaymentDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "GBP";
    public required string PaymentMethodId { get; init; }
    public required string Description { get; init; }
    public int ConcertId { get; init; }
    public Guid UserId { get; init; }
}

internal sealed record TicketTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Ticket;
    public int ConcertId { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

internal sealed record SettlementTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Settlement;
    public int BookingId { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

internal sealed record VerifyTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Verify;
    public int ApplicationId { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
