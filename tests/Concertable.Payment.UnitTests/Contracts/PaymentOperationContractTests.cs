extern alias PaymentClient;

using Concertable.Messaging.Contracts;
using Concertable.Payment.Contracts.Events;
using Concertable.Testing;
using Google.Protobuf.Reflection;
using ClientSnapshot = PaymentClient::Concertable.Payment.Client.PaymentOperationSnapshot;
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
        AssertValues<PaymentOperationState>(1, 2, 3, 4, 5, 6, 7, 8, 9);
        AssertValues<PaymentOperationTerminalDisposition>(1, 2, 3);
        AssertValues<PaymentOperationRetryDisposition>(1, 2, 3, 4, 5, 6);
        AssertValues<PaymentOperationFailureCode>(1, 2, 3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void ProtoEnums_HaveStableValuesWithUnspecifiedZero()
    {
        AssertValues<Proto.PaymentSessionKind>(0, 1, 2, 3, 4);
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
    }

    [Theory]
    [InlineData(typeof(PaymentOperationIdentity))]
    [InlineData(typeof(PaymentOperationStateChanged))]
    [InlineData(typeof(ClientSnapshot))]
    public void PublishedVocabulary_DoesNotReferenceProviderOrConsumerRuntime(Type type) =>
        Assert.Empty(type.Assembly
            .ReferencesToAssembliesStartingWith("Stripe", "Concertable.B2B", "Concertable.Customer"));

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
