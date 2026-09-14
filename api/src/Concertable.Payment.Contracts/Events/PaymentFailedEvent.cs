using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts.Events;

[MessageType("concertable.payment.payment-failed.v1")]
public sealed record PaymentFailedEvent(
    PaymentOperationReference Reference,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
