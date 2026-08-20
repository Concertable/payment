extern alias PaymentClient;

using Concertable.Payment.Contracts;
using Google.Protobuf.WellKnownTypes;
using PaymentClient::Concertable.Payment.Client.Adapters;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentOperationMappersTests
{
    public static TheoryData<Proto.PaymentSessionKind, PaymentSessionKind> SessionKinds => new()
    {
        { Proto.PaymentSessionKind.Payment, PaymentSessionKind.Payment },
        { Proto.PaymentSessionKind.Authorization, PaymentSessionKind.Authorization },
        { Proto.PaymentSessionKind.PaymentMethodSetup, PaymentSessionKind.PaymentMethodSetup },
        { Proto.PaymentSessionKind.PaymentMethodVerification, PaymentSessionKind.PaymentMethodVerification }
    };

    public static TheoryData<Proto.PaymentOperationState, PaymentOperationState> States => new()
    {
        { Proto.PaymentOperationState.Creating, PaymentOperationState.Creating },
        { Proto.PaymentOperationState.RequiresPaymentMethod, PaymentOperationState.RequiresPaymentMethod },
        { Proto.PaymentOperationState.RequiresConfirmation, PaymentOperationState.RequiresConfirmation },
        { Proto.PaymentOperationState.RequiresAction, PaymentOperationState.RequiresAction },
        { Proto.PaymentOperationState.Processing, PaymentOperationState.Processing },
        { Proto.PaymentOperationState.Authorized, PaymentOperationState.Authorized },
        { Proto.PaymentOperationState.Succeeded, PaymentOperationState.Succeeded },
        { Proto.PaymentOperationState.Canceled, PaymentOperationState.Canceled },
        { Proto.PaymentOperationState.Failed, PaymentOperationState.Failed }
    };

    public static TheoryData<Proto.PaymentOperationTerminalDisposition, PaymentOperationTerminalDisposition> TerminalDispositions => new()
    {
        { Proto.PaymentOperationTerminalDisposition.NonTerminal, PaymentOperationTerminalDisposition.NonTerminal },
        { Proto.PaymentOperationTerminalDisposition.AttemptTerminal, PaymentOperationTerminalDisposition.AttemptTerminal },
        { Proto.PaymentOperationTerminalDisposition.OperationTerminal, PaymentOperationTerminalDisposition.OperationTerminal }
    };

    public static TheoryData<Proto.PaymentOperationRetryDisposition, PaymentOperationRetryDisposition> RetryDispositions => new()
    {
        { Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt, PaymentOperationRetryDisposition.ContinueCurrentAttempt },
        { Proto.PaymentOperationRetryDisposition.RetryCurrentAttempt, PaymentOperationRetryDisposition.RetryCurrentAttempt },
        { Proto.PaymentOperationRetryDisposition.CreateNewAttempt, PaymentOperationRetryDisposition.CreateNewAttempt },
        { Proto.PaymentOperationRetryDisposition.CreateNewOperation, PaymentOperationRetryDisposition.CreateNewOperation },
        { Proto.PaymentOperationRetryDisposition.Reconcile, PaymentOperationRetryDisposition.Reconcile },
        { Proto.PaymentOperationRetryDisposition.NotRetryable, PaymentOperationRetryDisposition.NotRetryable }
    };

    public static TheoryData<Proto.PaymentOperationFailureCode, PaymentOperationFailureCode> FailureCodes => new()
    {
        { Proto.PaymentOperationFailureCode.PaymentMethodRequired, PaymentOperationFailureCode.PaymentMethodRequired },
        { Proto.PaymentOperationFailureCode.AuthenticationRequired, PaymentOperationFailureCode.AuthenticationRequired },
        { Proto.PaymentOperationFailureCode.Declined, PaymentOperationFailureCode.Declined },
        { Proto.PaymentOperationFailureCode.Expired, PaymentOperationFailureCode.Expired },
        { Proto.PaymentOperationFailureCode.Canceled, PaymentOperationFailureCode.Canceled },
        { Proto.PaymentOperationFailureCode.OperationConflict, PaymentOperationFailureCode.OperationConflict },
        { Proto.PaymentOperationFailureCode.ProviderUnavailable, PaymentOperationFailureCode.ProviderUnavailable },
        { Proto.PaymentOperationFailureCode.Unknown, PaymentOperationFailureCode.Unknown }
    };

    [Theory]
    [MemberData(nameof(SessionKinds))]
    public void ToPaymentSessionKind_KnownValue_ReturnsContract(
        Proto.PaymentSessionKind source,
        PaymentSessionKind expected) =>
        Assert.Equal(expected, source.ToPaymentSessionKind());

    [Theory]
    [MemberData(nameof(States))]
    public void ToPaymentOperationState_KnownValue_ReturnsContract(
        Proto.PaymentOperationState source,
        PaymentOperationState expected) =>
        Assert.Equal(expected, source.ToPaymentOperationState());

    [Theory]
    [MemberData(nameof(TerminalDispositions))]
    public void ToPaymentOperationTerminalDisposition_KnownValue_ReturnsContract(
        Proto.PaymentOperationTerminalDisposition source,
        PaymentOperationTerminalDisposition expected) =>
        Assert.Equal(expected, source.ToPaymentOperationTerminalDisposition());

    [Theory]
    [MemberData(nameof(RetryDispositions))]
    public void ToPaymentOperationRetryDisposition_KnownValue_ReturnsContract(
        Proto.PaymentOperationRetryDisposition source,
        PaymentOperationRetryDisposition expected) =>
        Assert.Equal(expected, source.ToPaymentOperationRetryDisposition());

    [Theory]
    [MemberData(nameof(FailureCodes))]
    public void ToPaymentOperationFailureCode_KnownValue_ReturnsContract(
        Proto.PaymentOperationFailureCode source,
        PaymentOperationFailureCode expected) =>
        Assert.Equal(expected, source.ToPaymentOperationFailureCode());

    [Theory]
    [MemberData(nameof(FailureCodes))]
    public void ToPaymentOperationFailure_KnownCode_DerivesPublishedMessage(
        Proto.PaymentOperationFailureCode source,
        PaymentOperationFailureCode expected)
    {
        var failure = new Proto.PaymentOperationFailure
        {
            Code = source,
            Message = nameof(ToPaymentOperationFailure_KnownCode_DerivesPublishedMessage)
        };

        var mapped = failure.ToPaymentOperationFailure();

        Assert.Equal(PaymentOperationFailure.FromCode(expected), mapped);
        Assert.NotEqual(failure.Message, mapped.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void ToPaymentOperationFailure_UnknownCode_Throws(int code)
    {
        var failure = new Proto.PaymentOperationFailure
        {
            Code = (Proto.PaymentOperationFailureCode)code,
            Message = nameof(ToPaymentOperationFailure_UnknownCode_Throws)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => failure.ToPaymentOperationFailure());
    }

    [Fact]
    public void EnumMappers_UnspecifiedValues_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.PaymentSessionKind.Unspecified.ToPaymentSessionKind());
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.PaymentOperationState.Unspecified.ToPaymentOperationState());
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.PaymentOperationTerminalDisposition.Unspecified.ToPaymentOperationTerminalDisposition());
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.PaymentOperationRetryDisposition.Unspecified.ToPaymentOperationRetryDisposition());
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.PaymentOperationFailureCode.Unspecified.ToPaymentOperationFailureCode());
    }

    [Fact]
    public void ToPaymentSessionDescriptor_OptionalValuesMissing_ReturnsNulls()
    {
        var source = new Proto.PaymentSessionDescriptor
        {
            Identity = Identity(),
            Kind = Proto.PaymentSessionKind.Payment,
            ClientSecret = "secret"
        };

        var descriptor = source.ToPaymentSessionDescriptor();

        Assert.Null(descriptor.CustomerSessionSecret);
        Assert.Null(descriptor.CustomerToken);
    }

    [Fact]
    public void ToPaymentOperationSnapshot_PopulatedMessage_ReturnsContract()
    {
        var expiresAt = DateTimeOffset.Parse("2026-08-17T10:00:00Z");
        var captureBefore = DateTimeOffset.Parse("2026-08-18T10:00:00Z");
        var source = new Proto.PaymentOperationSnapshot
        {
            Identity = Identity(),
            State = Proto.PaymentOperationState.Authorized,
            TerminalDisposition = Proto.PaymentOperationTerminalDisposition.NonTerminal,
            RetryDisposition = Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            ExpiresAt = Timestamp.FromDateTimeOffset(expiresAt),
            CaptureBefore = Timestamp.FromDateTimeOffset(captureBefore),
            Failure = new Proto.PaymentOperationFailure
            {
                Code = Proto.PaymentOperationFailureCode.AuthenticationRequired,
                Message = "Payment authentication is required."
            }
        };

        var snapshot = source.ToPaymentOperationSnapshot();

        Assert.Equal(expiresAt, snapshot.ExpiresAt);
        Assert.Equal(captureBefore, snapshot.CaptureBefore);
        Assert.Equal(
            new PaymentOperationFailure(
                PaymentOperationFailureCode.AuthenticationRequired,
                "Payment authentication is required."),
            snapshot.Failure);
    }

    private static Proto.PaymentOperationIdentity Identity() => new()
    {
        OperationId = "0198b732-42a0-7000-8000-000000000001",
        AttemptId = "0198b732-42a0-7000-8000-000000000002",
        Revision = 3
    };
}
