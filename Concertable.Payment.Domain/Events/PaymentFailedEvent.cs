using Concertable.Messaging;
using Concertable.Shared;

namespace Concertable.Payment.Domain.Events;

public record PaymentFailedEvent(
    string TransactionId,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
