using Concertable.Payment.Application.Interfaces;

namespace Concertable.Payment.Application.DTOs;

internal sealed record PaymentMethodDto(string Brand, string Last4, int ExpMonth, int ExpYear);

internal sealed record PaymentTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Payment;
    public required string OperationType { get; init; }
    public required string ClientReference { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record SettlementTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Settlement;
    public required string OperationType { get; init; }
    public required string ClientReference { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public long PlatformFee { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record VerifyTransactionDto : ITransaction
{
    public int Id { get; init; }
    public TransactionType TransactionType => TransactionType.Verify;
    public required string OperationType { get; init; }
    public required string ClientReference { get; init; }
    public Guid PayerId { get; init; }
    public Guid PayeeId { get; init; }
    public required string PaymentIntentId { get; init; }
    public long Amount { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

internal sealed record EscrowDto(
    int Id,
    PaymentOperationReference Reference,
    Guid FromOwnerId,
    Guid ToOwnerId,
    decimal Amount,
    EscrowStatus Status,
    string ChargeId,
    string? TransferId,
    DateTime? ReleasedAt);
