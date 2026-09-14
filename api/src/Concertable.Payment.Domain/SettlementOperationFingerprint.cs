using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Concertable.Payment.Domain;

internal readonly record struct SettlementOperationFingerprint
{
    private SettlementOperationFingerprint(int version, string value)
    {
        Version = version;
        Value = value;
    }

    internal const int CurrentVersion = 2;

    public int Version { get; }
    public string Value { get; }

    internal static SettlementOperationFingerprint CreateCharge(
        Guid operationId,
        Guid payerId,
        Guid payeeId,
        Money amount,
        Money platformFee,
        string paymentMethodId,
        PaymentSession session,
        PaymentOperationReference reference)
    {
        ValidateOperationId(operationId);
        DomainException.ThrowIfNullOrWhiteSpace(paymentMethodId, "Settlement payment method");

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("operation", "charge");
            writer.WriteString("operationId", operationId.ToString("N"));
            writer.WriteString("payerId", payerId.ToString("N"));
            writer.WriteString("payeeId", payeeId.ToString("N"));
            writer.WriteNumber("amountMinor", amount.ToMinorUnits());
            writer.WriteString("currency", amount.Currency.ToString().ToUpperInvariant());
            writer.WriteNumber("platformFeeMinor", platformFee.ToMinorUnits());
            writer.WriteString("paymentMethodId", paymentMethodId.Trim());
            writer.WriteString("session", session.ToString());
            writer.WriteString("operationType", reference.OperationType);
            writer.WriteString("clientReference", reference.ClientReference);
            writer.WriteEndObject();
        }

        return new(CurrentVersion, Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)));
    }

    internal static SettlementOperationFingerprint CreateRelease(
        Guid operationId,
        EscrowEntity escrow)
    {
        ValidateOperationId(operationId);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteString("operation", "release");
            writer.WriteString("operationId", operationId.ToString("N"));
            writer.WriteString("operationType", escrow.OperationType);
            writer.WriteString("clientReference", escrow.ClientReference);
            writer.WriteNumber("escrowId", escrow.Id);
            writer.WriteString("payeeId", escrow.ToOwnerId.ToString("N"));
            writer.WriteNumber("amountMinor", escrow.PayeeGrossMinor);
            writer.WriteString("currency", escrow.Currency.ToString().ToUpperInvariant());
            writer.WriteString("chargeId", escrow.ChargeId);
            if (escrow.CommissionBindingId is { } commissionBindingId)
                writer.WriteString("commissionBindingId", commissionBindingId.ToString("N"));
            else
                writer.WriteNull("commissionBindingId");
            writer.WriteEndObject();
        }

        return new(CurrentVersion, Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)));
    }

    private static void ValidateOperationId(Guid operationId)
    {
        if (operationId == Guid.Empty)
            throw new DomainException("Settlement operation id is required.");
        if (operationId.Version != 7)
            throw new DomainException("Settlement operation id must be UUIDv7.");
    }
}
