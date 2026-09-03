namespace Concertable.Payment.Contracts;

public sealed record PaymentOperationReference(
    string OperationType,
    string ConsumerCorrelation);

public sealed record PaymentMethodSetupRequest(
    PaymentOperationReference Reference,
    PaymentSessionKind Kind,
    Guid PayerOwnerId);

public sealed record PaymentMethodValidationRequest(
    PaymentOperationReference Reference,
    Guid PayerOwnerId);
