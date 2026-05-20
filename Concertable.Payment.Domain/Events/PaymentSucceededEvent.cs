using Concertable.Messaging;
using Concertable.Shared;

namespace Concertable.Payment.Domain.Events;

public record PaymentSucceededEvent(
    string TransactionId,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
