using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts.Events;

[MessageType("concertable.payment.payment-method-owner-registered.v1")]
public sealed record PaymentMethodOwnerRegisteredEvent(
    Guid OwnerId,
    string Email) : IIntegrationEvent;
