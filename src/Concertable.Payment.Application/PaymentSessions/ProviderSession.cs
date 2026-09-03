using Concertable.Payment.Domain.ProviderContract;

namespace Concertable.Payment.Application.PaymentSessions;

internal sealed record ProviderSession(
    PaymentSessionProviderObjectKind ProviderObjectKind,
    string ProviderObjectId,
    string Status,
    DateTimeOffset ObservedAt,
    DateTimeOffset? CaptureBefore,
    ProviderFailureClassification? FailureClassification,
    bool IsExplicitConsumerCancellation,
    bool CanCancel,
    string? ClientSecret,
    string? PaymentMethodId,
    string? ProviderRequestId,
    string? ProviderDiagnosticCode,
    string? ProviderDiagnosticMessage);
