using System.Text.Json.Serialization;

namespace Concertable.Payment.Application.Interfaces;

[JsonDerivedType(typeof(TicketTransactionDto), TransactionTypes.Ticket)]
[JsonDerivedType(typeof(SettlementTransactionDto), TransactionTypes.Settlement)]
[JsonDerivedType(typeof(VerifyTransactionDto), TransactionTypes.Verify)]
internal interface ITransaction
{
    int Id { get; set; }
    TransactionType TransactionType { get; }
    Guid FromUserId { get; set; }
    Guid ToUserId { get; set; }
    string PaymentIntentId { get; set; }
    long Amount { get; set; }
    TransactionStatus Status { get; set; }
    DateTime CreatedAt { get; set; }
}
