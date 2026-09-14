using System.Text.Json.Serialization;

namespace Concertable.Payment.Application.Interfaces;

[JsonDerivedType(typeof(PaymentTransactionDto), TransactionTypes.Payment)]
[JsonDerivedType(typeof(SettlementTransactionDto), TransactionTypes.Settlement)]
[JsonDerivedType(typeof(VerifyTransactionDto), TransactionTypes.Verify)]
internal interface ITransaction
{
    int Id { get; }
    TransactionType TransactionType { get; }
    Guid PayerId { get; }
    Guid PayeeId { get; }
    string PaymentIntentId { get; }
    long Amount { get; }
    TransactionStatus Status { get; }
    string OperationType { get; }
    string ClientReference { get; }
    DateTimeOffset CreatedAt { get; }
}
