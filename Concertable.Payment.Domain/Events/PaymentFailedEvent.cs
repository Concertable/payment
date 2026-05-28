using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Domain.Events;

[MessageType("concertable.payment.payment-failed.v1")]
public record PaymentFailedEvent(
    string TransactionId,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
