using System.Text.Json.Serialization;

namespace Concertable.Payment.Application.Interfaces;

[JsonDerivedType(typeof(TicketTransactionDto), TransactionTypes.Ticket)]
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
    DateTime CreatedAt { get; }
}
