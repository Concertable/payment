extern alias PaymentClient;

using Concertable.Messaging.Contracts;
using Concertable.Payment.Contracts.Events;
using Google.Protobuf.Reflection;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentOperationContractTests
{
    [Fact]
    public void StateChanged_MessageTypeReturnsPublishedContract() =>
        Assert.Equal(
            "concertable.payment.payment-operation-state-changed.v1",
            MessageTypeAttribute.Resolve(typeof(PaymentOperationStateChanged)));

    [Fact]
    public void ContractEnums_HaveStableNonZeroValues()
    {
        AssertValues<PaymentSessionKind>(1, 2, 3, 4);
        AssertValues<PaymentSessionFundsRouting>(1, 2, 3);
        AssertValues<PaymentOperationState>(1, 2, 3, 4, 5, 6, 7, 8, 9);
        AssertValues<PaymentOperationTerminalDisposition>(1, 2, 3);
        AssertValues<PaymentOperationRetryDisposition>(1, 2, 3, 4, 5, 6);
        AssertValues<PaymentOperationFailureCode>(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void ProtoEnums_HaveStableValuesWithUnspecifiedZero()
    {
        AssertValues<Proto.PaymentSessionKind>(0, 1, 2, 3, 4);
        AssertValues<Proto.PaymentSessionFundsRouting>(0, 1, 2, 3);
        AssertValues<Proto.PaymentOperationState>(0, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        AssertValues<Proto.PaymentOperationTerminalDisposition>(0, 1, 2, 3);
        AssertValues<Proto.PaymentOperationRetryDisposition>(0, 1, 2, 3, 4, 5, 6);
        AssertValues<Proto.PaymentOperationFailureCode>(0, 1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void ProtoMessages_HaveStableFieldNumbersAndTypes()
    {
        AssertFields(
            Proto.PaymentOperationIdentity.Descriptor,
            ("operation_id", 1, FieldType.String),
            ("attempt_id", 2, FieldType.String),
            ("revision", 3, FieldType.Int64));
        AssertFields(
            Proto.PaymentOperationFailure.Descriptor,
            ("code", 1, FieldType.Enum),
            ("message", 2, FieldType.String));
        AssertFields(
            Proto.PaymentSessionDescriptor.Descriptor,
            ("identity", 1, FieldType.Message),
            ("kind", 2, FieldType.Enum),
            ("client_secret", 3, FieldType.String),
            ("customer_session_secret", 4, FieldType.String),
            ("customer_token", 5, FieldType.String));
        AssertFields(
            Proto.PaymentOperationSnapshot.Descriptor,
            ("identity", 1, FieldType.Message),
            ("state", 2, FieldType.Enum),
            ("terminal_disposition", 3, FieldType.Enum),
            ("retry_disposition", 4, FieldType.Enum),
            ("expires_at", 5, FieldType.Message),
            ("capture_before", 6, FieldType.Message),
            ("failure", 7, FieldType.Message));
        AssertFields(
            Proto.PaymentOperationReference.Descriptor,
            ("operation_type", 1, FieldType.String),
            ("client_reference", 2, FieldType.String));
        AssertFields(
            Proto.PaymentMethodSetupRequest.Descriptor,
            ("reference", 1, FieldType.Message),
            ("kind", 2, FieldType.Enum),
            ("payer_owner_id", 3, FieldType.String),
            ("mandate_terms_version", 4, FieldType.String));
        AssertFields(
            Proto.PaymentMethodValidationRequest.Descriptor,
            ("reference", 1, FieldType.Message),
            ("payer_owner_id", 2, FieldType.String));
        AssertFields(
            Proto.PaymentMethodSetupResponse.Descriptor,
            ("client_secret", 1, FieldType.String),
            ("customer_session_secret", 2, FieldType.String),
            ("customer_token", 3, FieldType.String));
        AssertFields(
            Proto.PaymentSessionOperationRequest.Descriptor,
            ("operation_id", 1, FieldType.String),
            ("kind", 2, FieldType.Enum),
            ("operation_type", 3, FieldType.String),
            ("client_reference", 4, FieldType.String),
            ("payer_owner_id", 5, FieldType.String),
            ("payee_owner_id", 6, FieldType.String),
            ("amount_minor", 7, FieldType.Int64),
            ("currency", 8, FieldType.Enum),
            ("funds_routing", 9, FieldType.Enum),
            ("session", 10, FieldType.Enum),
            ("mandate_terms_version", 12, FieldType.String));
        AssertFields(
            Proto.PaymentSessionRetryRequest.Descriptor,
            ("operation_id", 1, FieldType.String),
            ("expected_attempt_id", 2, FieldType.String),
            ("expected_revision", 3, FieldType.Int64),
            ("owner_id", 4, FieldType.String));
        AssertFields(
            Proto.PaymentSessionStatusRequest.Descriptor,
            ("operation_id", 1, FieldType.String),
            ("owner_id", 2, FieldType.String));
    }

    [Fact]
    public void ProtoService_HasStableSessionOperationMethods()
    {
        var service = Proto.PaymentSessionOperations.Descriptor;

        Assert.Equal(
            new[]
            {
                ("SetupPaymentMethod", "payment.PaymentMethodSetupRequest", "payment.PaymentMethodSetupResponse"),
                ("ValidatePaymentMethod", "payment.PaymentMethodValidationRequest", "google.protobuf.Empty"),
                ("Create", "payment.PaymentSessionOperationRequest", "payment.PaymentSessionDescriptor"),
                ("Retry", "payment.PaymentSessionRetryRequest", "payment.PaymentSessionDescriptor"),
                ("GetStatus", "payment.PaymentSessionStatusRequest", "payment.PaymentOperationSnapshot")
            },
            service.Methods.Select(method =>
                (method.Name, method.InputType.FullName, method.OutputType.FullName)));
    }

    private static void AssertValues<TEnum>(params int[] expected)
        where TEnum : struct, Enum =>
        Assert.Equal(expected, Enum.GetValues<TEnum>().Select(value => Convert.ToInt32(value)));

    private static void AssertFields(
        MessageDescriptor descriptor,
        params (string Name, int Number, FieldType Type)[] expected) =>
        Assert.Equal(
            expected,
            descriptor.Fields.InFieldNumberOrder()
                .Select(field => (field.Name, field.FieldNumber, field.FieldType)));
}
