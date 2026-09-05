using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts.Events;

[MessageType("concertable.payment.payment-succeeded.v1")]
public sealed record PaymentSucceededEvent(
    PaymentOperationReference Reference,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
