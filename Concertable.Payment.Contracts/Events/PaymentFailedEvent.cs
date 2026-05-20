using Concertable.Messaging;
using Concertable.Shared;

namespace Concertable.Payment.Contracts.Events;

public record PaymentFailedEvent(
    string TransactionId,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent;
