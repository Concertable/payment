using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Domain.Events;

[MessageType("concertable.payment.payment-succeeded.v1")]
public record PaymentSucceededEvent(
    string TransactionId,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
