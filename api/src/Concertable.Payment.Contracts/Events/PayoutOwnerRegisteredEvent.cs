using Concertable.Messaging.Contracts;

namespace Concertable.Payment.Contracts.Events;

[MessageType("concertable.payment.payout-owner-registered.v1")]
public sealed record PayoutOwnerRegisteredEvent(
    Guid OwnerId,
    string Email) : IIntegrationEvent;
