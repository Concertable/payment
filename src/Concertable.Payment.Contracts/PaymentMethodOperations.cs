using System.Text.Json.Serialization;

namespace Concertable.Payment.Contracts;

public readonly record struct PaymentOperationReference
{
    public const int MaxOperationTypeLength = 100;
    public const int MaxClientReferenceLength = 200;

    [JsonConstructor]
    public PaymentOperationReference(string operationType, string clientReference)
    {
        this.OperationType = Normalize(operationType, nameof(operationType), MaxOperationTypeLength);
        this.ClientReference = Normalize(clientReference, nameof(clientReference), MaxClientReferenceLength);
    }

    public string OperationType { get; }
    public string ClientReference { get; }

    public PaymentOperationReference EnsureValid() => new(OperationType, ClientReference);

    private static string Normalize(string value, string paramName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(paramName, $"Value cannot exceed {maxLength} characters.");
        return value;
    }
}

public sealed record PaymentMethodSetupRequest(
    PaymentOperationReference Reference,
    PaymentSessionKind Kind,
    Guid PayerOwnerId,
    string MandateTermsVersion);

public sealed record PaymentMethodValidationRequest(
    PaymentOperationReference Reference,
    Guid PayerOwnerId);
