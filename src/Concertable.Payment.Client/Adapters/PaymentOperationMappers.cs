using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentOperationMappers
{
    extension(Proto.PaymentSessionDescriptor descriptor)
    {
        public PaymentSessionDescriptor ToPaymentSessionDescriptor() =>
            new(
                descriptor.Identity.ToPaymentOperationIdentity(),
                descriptor.Kind.ToPaymentSessionKind(),
                descriptor.ClientSecret,
                EmptyToNull(descriptor.CustomerSessionSecret),
                EmptyToNull(descriptor.CustomerToken));
    }

    extension(Proto.PaymentOperationSnapshot snapshot)
    {
        public PaymentOperationSnapshot ToPaymentOperationSnapshot() =>
            new(
                snapshot.Identity.ToPaymentOperationIdentity(),
                snapshot.State.ToPaymentOperationState(),
                snapshot.TerminalDisposition.ToPaymentOperationTerminalDisposition(),
                snapshot.RetryDisposition.ToPaymentOperationRetryDisposition(),
                snapshot.ExpiresAt?.ToDateTimeOffset(),
                snapshot.CaptureBefore?.ToDateTimeOffset(),
                snapshot.Failure?.ToPaymentOperationFailure());
    }

    extension(Proto.PaymentOperationIdentity identity)
    {
        public PaymentOperationIdentity ToPaymentOperationIdentity() =>
            new(Guid.Parse(identity.OperationId), Guid.Parse(identity.AttemptId), identity.Revision);
    }

    extension(Proto.PaymentOperationFailure failure)
    {
        public PaymentOperationFailure ToPaymentOperationFailure() =>
            PaymentOperationFailure.FromCode(failure.Code.ToPaymentOperationFailureCode());
    }

    extension(Proto.PaymentSessionKind kind)
    {
        public PaymentSessionKind ToPaymentSessionKind() => kind switch
        {
            Proto.PaymentSessionKind.Payment => PaymentSessionKind.Payment,
            Proto.PaymentSessionKind.Authorization => PaymentSessionKind.Authorization,
            Proto.PaymentSessionKind.PaymentMethodSetup => PaymentSessionKind.PaymentMethodSetup,
            Proto.PaymentSessionKind.PaymentMethodVerification => PaymentSessionKind.PaymentMethodVerification,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    extension(Proto.PaymentOperationState state)
    {
        public PaymentOperationState ToPaymentOperationState() => state switch
        {
            Proto.PaymentOperationState.Creating => PaymentOperationState.Creating,
            Proto.PaymentOperationState.RequiresPaymentMethod => PaymentOperationState.RequiresPaymentMethod,
            Proto.PaymentOperationState.RequiresConfirmation => PaymentOperationState.RequiresConfirmation,
            Proto.PaymentOperationState.RequiresAction => PaymentOperationState.RequiresAction,
            Proto.PaymentOperationState.Processing => PaymentOperationState.Processing,
            Proto.PaymentOperationState.Authorized => PaymentOperationState.Authorized,
            Proto.PaymentOperationState.Succeeded => PaymentOperationState.Succeeded,
            Proto.PaymentOperationState.Canceled => PaymentOperationState.Canceled,
            Proto.PaymentOperationState.Failed => PaymentOperationState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    extension(Proto.PaymentOperationTerminalDisposition disposition)
    {
        public PaymentOperationTerminalDisposition ToPaymentOperationTerminalDisposition() => disposition switch
        {
            Proto.PaymentOperationTerminalDisposition.NonTerminal => PaymentOperationTerminalDisposition.NonTerminal,
            Proto.PaymentOperationTerminalDisposition.AttemptTerminal => PaymentOperationTerminalDisposition.AttemptTerminal,
            Proto.PaymentOperationTerminalDisposition.OperationTerminal => PaymentOperationTerminalDisposition.OperationTerminal,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };
    }

    extension(Proto.PaymentOperationRetryDisposition disposition)
    {
        public PaymentOperationRetryDisposition ToPaymentOperationRetryDisposition() => disposition switch
        {
            Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt => PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            Proto.PaymentOperationRetryDisposition.RetryCurrentAttempt => PaymentOperationRetryDisposition.RetryCurrentAttempt,
            Proto.PaymentOperationRetryDisposition.CreateNewAttempt => PaymentOperationRetryDisposition.CreateNewAttempt,
            Proto.PaymentOperationRetryDisposition.CreateNewOperation => PaymentOperationRetryDisposition.CreateNewOperation,
            Proto.PaymentOperationRetryDisposition.Reconcile => PaymentOperationRetryDisposition.Reconcile,
            Proto.PaymentOperationRetryDisposition.NotRetryable => PaymentOperationRetryDisposition.NotRetryable,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };
    }

    extension(Proto.PaymentOperationFailureCode code)
    {
        public PaymentOperationFailureCode ToPaymentOperationFailureCode() => code switch
        {
            Proto.PaymentOperationFailureCode.PaymentMethodRequired => PaymentOperationFailureCode.PaymentMethodRequired,
            Proto.PaymentOperationFailureCode.AuthenticationRequired => PaymentOperationFailureCode.AuthenticationRequired,
            Proto.PaymentOperationFailureCode.Declined => PaymentOperationFailureCode.Declined,
            Proto.PaymentOperationFailureCode.Expired => PaymentOperationFailureCode.Expired,
            Proto.PaymentOperationFailureCode.Canceled => PaymentOperationFailureCode.Canceled,
            Proto.PaymentOperationFailureCode.OperationConflict => PaymentOperationFailureCode.OperationConflict,
            Proto.PaymentOperationFailureCode.ProviderUnavailable => PaymentOperationFailureCode.ProviderUnavailable,
            Proto.PaymentOperationFailureCode.Unknown => PaymentOperationFailureCode.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };
    }

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
}
