using Concertable.Messaging;
using Concertable.Shared;

namespace Concertable.Payment.Contracts.Events;

public record PaymentSucceededEvent(
    string TransactionId,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
