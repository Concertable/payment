namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record PaymentSessionProviderEventEvidence(
    string ProviderEventId,
    DateTimeOffset ProviderEventCreatedAt);
