using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Concertable.Payment.Domain;

internal readonly record struct PaymentSessionFingerprint
{
    private PaymentSessionFingerprint(int version, string value)
    {
        Version = version;
        Value = value;
    }

    internal const int CurrentVersion = 1;

    public int Version { get; }
    public string Value { get; }

    internal static PaymentSessionFingerprint Create(PaymentSessionSpecification specification) =>
        Create(specification, CurrentVersion);

    internal static PaymentSessionFingerprint Create(
        PaymentSessionSpecification specification,
        int version)
    {
        if (version != CurrentVersion)
            throw new DomainException($"Payment session fingerprint version {version} is not supported.");

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", version);
            writer.WriteString("operationId", specification.OperationId.ToString("N"));
            writer.WriteString("sessionKind", specification.SessionKind.ToString());
            writer.WriteString("session", specification.Session.ToString());
            writer.WriteString("operationType", specification.OperationType);
            writer.WriteString("consumerCorrelation", specification.ConsumerCorrelation);
            writer.WriteString("payerOwnerKey", specification.PayerOwnerKey);
            WriteNullable(writer, "payeeOwnerKey", specification.PayeeOwnerKey);
            if (specification.AmountMinor is { } amountMinor)
                writer.WriteNumber("amountMinor", amountMinor);
            else
                writer.WriteNull("amountMinor");
            if (specification.Currency is { } currency)
                writer.WriteString("currency", currency.ToString().ToUpperInvariant());
            else
                writer.WriteNull("currency");
            writer.WriteString("fundsRouting", specification.FundsRouting.ToString());
            writer.WriteString("captureMode", specification.CaptureMode.ToString());
            WriteNullable(writer, "paymentMethodId", specification.PaymentMethodId);
            writer.WriteString("providerCustomerId", specification.ProviderCustomerId);
            WriteNullable(
                writer,
                "providerConnectedAccountId",
                specification.ProviderConnectedAccountId);
            WriteNullable(writer, "mandateTermsVersion", specification.MandateTermsVersion);
            writer.WriteEndObject();
        }

        return new(version, Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)));
    }

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
            writer.WriteNull(name);
        else
            writer.WriteString(name, value);
    }
}
