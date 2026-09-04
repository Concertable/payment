namespace Concertable.Payment.Contracts;

public readonly record struct PaymentOperationReference(
    string OperationType,
    string ClientReference);

public sealed record PaymentMethodSetupRequest(
    PaymentOperationReference Reference,
    PaymentSessionKind Kind,
    Guid PayerOwnerId,
    string MandateTermsVersion);

public sealed record PaymentMethodValidationRequest(
    PaymentOperationReference Reference,
    Guid PayerOwnerId);
