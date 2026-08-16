using System.Collections.Frozen;
using Concertable.Payment.Contracts;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentOperationMappers
{
    private static readonly FrozenDictionary<Proto.PaymentSessionKind, PaymentSessionKind> sessionKinds =
        new Dictionary<Proto.PaymentSessionKind, PaymentSessionKind>
        {
            [Proto.PaymentSessionKind.Payment] = PaymentSessionKind.Payment,
            [Proto.PaymentSessionKind.Authorization] = PaymentSessionKind.Authorization,
            [Proto.PaymentSessionKind.PaymentMethodSetup] = PaymentSessionKind.PaymentMethodSetup,
            [Proto.PaymentSessionKind.PaymentMethodVerification] = PaymentSessionKind.PaymentMethodVerification
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Proto.PaymentOperationState, PaymentOperationState> operationStates =
        new Dictionary<Proto.PaymentOperationState, PaymentOperationState>
        {
            [Proto.PaymentOperationState.Creating] = PaymentOperationState.Creating,
            [Proto.PaymentOperationState.RequiresPaymentMethod] = PaymentOperationState.RequiresPaymentMethod,
            [Proto.PaymentOperationState.RequiresConfirmation] = PaymentOperationState.RequiresConfirmation,
            [Proto.PaymentOperationState.RequiresAction] = PaymentOperationState.RequiresAction,
            [Proto.PaymentOperationState.Processing] = PaymentOperationState.Processing,
            [Proto.PaymentOperationState.Authorized] = PaymentOperationState.Authorized,
            [Proto.PaymentOperationState.Succeeded] = PaymentOperationState.Succeeded,
            [Proto.PaymentOperationState.Canceled] = PaymentOperationState.Canceled,
            [Proto.PaymentOperationState.Failed] = PaymentOperationState.Failed
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Proto.PaymentOperationTerminalDisposition, PaymentOperationTerminalDisposition>
        terminalDispositions = new Dictionary<Proto.PaymentOperationTerminalDisposition, PaymentOperationTerminalDisposition>
        {
            [Proto.PaymentOperationTerminalDisposition.NonTerminal] = PaymentOperationTerminalDisposition.NonTerminal,
            [Proto.PaymentOperationTerminalDisposition.AttemptTerminal] = PaymentOperationTerminalDisposition.AttemptTerminal,
            [Proto.PaymentOperationTerminalDisposition.OperationTerminal] = PaymentOperationTerminalDisposition.OperationTerminal
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Proto.PaymentOperationRetryDisposition, PaymentOperationRetryDisposition>
        retryDispositions = new Dictionary<Proto.PaymentOperationRetryDisposition, PaymentOperationRetryDisposition>
        {
            [Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt] = PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            [Proto.PaymentOperationRetryDisposition.RetryCurrentAttempt] = PaymentOperationRetryDisposition.RetryCurrentAttempt,
            [Proto.PaymentOperationRetryDisposition.CreateNewAttempt] = PaymentOperationRetryDisposition.CreateNewAttempt,
            [Proto.PaymentOperationRetryDisposition.CreateNewOperation] = PaymentOperationRetryDisposition.CreateNewOperation,
            [Proto.PaymentOperationRetryDisposition.Reconcile] = PaymentOperationRetryDisposition.Reconcile,
            [Proto.PaymentOperationRetryDisposition.NotRetryable] = PaymentOperationRetryDisposition.NotRetryable
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Proto.PaymentOperationFailureCode, PaymentOperationFailureCode> failureCodes =
        new Dictionary<Proto.PaymentOperationFailureCode, PaymentOperationFailureCode>
        {
            [Proto.PaymentOperationFailureCode.PaymentMethodRequired] = PaymentOperationFailureCode.PaymentMethodRequired,
            [Proto.PaymentOperationFailureCode.AuthenticationRequired] = PaymentOperationFailureCode.AuthenticationRequired,
            [Proto.PaymentOperationFailureCode.Declined] = PaymentOperationFailureCode.Declined,
            [Proto.PaymentOperationFailureCode.Expired] = PaymentOperationFailureCode.Expired,
            [Proto.PaymentOperationFailureCode.Canceled] = PaymentOperationFailureCode.Canceled,
            [Proto.PaymentOperationFailureCode.OperationConflict] = PaymentOperationFailureCode.OperationConflict,
            [Proto.PaymentOperationFailureCode.ProviderUnavailable] = PaymentOperationFailureCode.ProviderUnavailable,
            [Proto.PaymentOperationFailureCode.Unknown] = PaymentOperationFailureCode.Unknown
        }.ToFrozenDictionary();

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
        public PaymentSessionKind ToPaymentSessionKind() => Map(kind, sessionKinds);
    }

    extension(Proto.PaymentOperationState state)
    {
        public PaymentOperationState ToPaymentOperationState() => Map(state, operationStates);
    }

    extension(Proto.PaymentOperationTerminalDisposition disposition)
    {
        public PaymentOperationTerminalDisposition ToPaymentOperationTerminalDisposition() =>
            Map(disposition, terminalDispositions);
    }

    extension(Proto.PaymentOperationRetryDisposition disposition)
    {
        public PaymentOperationRetryDisposition ToPaymentOperationRetryDisposition() =>
            Map(disposition, retryDispositions);
    }

    extension(Proto.PaymentOperationFailureCode code)
    {
        public PaymentOperationFailureCode ToPaymentOperationFailureCode() => Map(code, failureCodes);
    }

    private static TTarget Map<TSource, TTarget>(
        TSource source,
        FrozenDictionary<TSource, TTarget> mappings)
        where TSource : notnull =>
        mappings.TryGetValue(source, out var target)
            ? target
            : throw new ArgumentOutOfRangeException(nameof(source), source, null);

    private static string? EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
}
