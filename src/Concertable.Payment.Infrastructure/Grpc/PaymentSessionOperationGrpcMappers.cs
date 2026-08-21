using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ContractOperationRequest = Concertable.Payment.Contracts.PaymentSessionOperationRequest;
using ContractRetryRequest = Concertable.Payment.Contracts.PaymentSessionRetryRequest;
using ContractStatusRequest = Concertable.Payment.Contracts.PaymentSessionStatusRequest;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Infrastructure.Grpc;

internal static class PaymentSessionOperationGrpcMappers
{
    extension(Proto.PaymentSessionOperationRequest request)
    {
        public ContractOperationRequest ToContract() =>
            new(
                request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
                request.Kind.ToContract(),
                request.OperationType,
                request.ConsumerCorrelation,
                request.PayerOwnerId.ParseOrThrow<Guid>(nameof(request.PayerOwnerId)),
                request.HasPayeeOwnerId
                    ? request.PayeeOwnerId.ParseOrThrow<Guid>(nameof(request.PayeeOwnerId))
                    : null,
                request.HasAmountMinor ? request.AmountMinor : null,
                request.HasCurrency ? request.Currency.ToContract() : null,
                request.FundsRouting.ToContract());
    }

    extension(Proto.PaymentSessionRetryRequest request)
    {
        public ContractRetryRequest ToContract() =>
            new(
                request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
                request.ExpectedAttemptId.ParseOrThrow<Guid>(nameof(request.ExpectedAttemptId)),
                request.ExpectedRevision,
                request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)));
    }

    extension(Proto.PaymentSessionStatusRequest request)
    {
        public ContractStatusRequest ToContract() =>
            new(
                request.OperationId.ParseOrThrow<Guid>(nameof(request.OperationId)),
                request.OwnerId.ParseOrThrow<Guid>(nameof(request.OwnerId)));
    }

    extension(PaymentSessionExecution execution)
    {
        public Proto.PaymentSessionDescriptor ToProto() => new()
        {
            Identity = execution.Identity.ToProto(),
            Kind = execution.Kind.ToProto(),
            ClientSecret = execution.ClientSecret ?? string.Empty,
            CustomerSessionSecret = execution.CustomerSessionSecret ?? string.Empty,
            CustomerToken = execution.CustomerToken ?? string.Empty
        };
    }

    extension(PaymentSessionStatus status)
    {
        public Proto.PaymentOperationSnapshot ToProto()
        {
            var message = new Proto.PaymentOperationSnapshot
            {
                Identity = status.Identity.ToProto(),
                State = status.State.ToProto(),
                TerminalDisposition = status.TerminalDisposition.ToProto(),
                RetryDisposition = status.RetryDisposition.ToProto()
            };
            if (status.ExpiresAt is { } expiresAt)
                message.ExpiresAt = Timestamp.FromDateTimeOffset(expiresAt);
            if (status.CaptureBefore is { } captureBefore)
                message.CaptureBefore = Timestamp.FromDateTimeOffset(captureBefore);
            if (status.Failure is { } failure)
                message.Failure = failure.ToProto();

            return message;
        }
    }

    extension(PaymentOperationIdentity identity)
    {
        private Proto.PaymentOperationIdentity ToProto() => new()
        {
            OperationId = identity.OperationId.ToString("D"),
            AttemptId = identity.AttemptId.ToString("D"),
            Revision = identity.Revision
        };
    }

    extension(PaymentOperationFailure failure)
    {
        private Proto.PaymentOperationFailure ToProto() => new()
        {
            Code = failure.Code.ToProto(),
            Message = failure.Message
        };
    }

    extension(Proto.PaymentSessionKind kind)
    {
        private PaymentSessionKind ToContract() => kind switch
        {
            Proto.PaymentSessionKind.Payment => PaymentSessionKind.Payment,
            Proto.PaymentSessionKind.Authorization => PaymentSessionKind.Authorization,
            Proto.PaymentSessionKind.PaymentMethodSetup => PaymentSessionKind.PaymentMethodSetup,
            Proto.PaymentSessionKind.PaymentMethodVerification => PaymentSessionKind.PaymentMethodVerification,
            _ => Invalid<PaymentSessionKind>(nameof(kind), kind)
        };
    }

    extension(Proto.PaymentSessionFundsRouting fundsRouting)
    {
        private PaymentSessionFundsRouting ToContract() => fundsRouting switch
        {
            Proto.PaymentSessionFundsRouting.None => PaymentSessionFundsRouting.None,
            Proto.PaymentSessionFundsRouting.Platform => PaymentSessionFundsRouting.Platform,
            Proto.PaymentSessionFundsRouting.Destination => PaymentSessionFundsRouting.Destination,
            _ => Invalid<PaymentSessionFundsRouting>(nameof(fundsRouting), fundsRouting)
        };
    }

    extension(Proto.Currency currency)
    {
        private Currency ToContract() => currency switch
        {
            Proto.Currency.Gbp => Currency.Gbp,
            _ => Invalid<Currency>(nameof(currency), currency)
        };
    }

    extension(PaymentSessionKind kind)
    {
        private Proto.PaymentSessionKind ToProto() => kind switch
        {
            PaymentSessionKind.Payment => Proto.PaymentSessionKind.Payment,
            PaymentSessionKind.Authorization => Proto.PaymentSessionKind.Authorization,
            PaymentSessionKind.PaymentMethodSetup => Proto.PaymentSessionKind.PaymentMethodSetup,
            PaymentSessionKind.PaymentMethodVerification => Proto.PaymentSessionKind.PaymentMethodVerification,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    extension(PaymentOperationState state)
    {
        private Proto.PaymentOperationState ToProto() => state switch
        {
            PaymentOperationState.Creating => Proto.PaymentOperationState.Creating,
            PaymentOperationState.RequiresPaymentMethod => Proto.PaymentOperationState.RequiresPaymentMethod,
            PaymentOperationState.RequiresConfirmation => Proto.PaymentOperationState.RequiresConfirmation,
            PaymentOperationState.RequiresAction => Proto.PaymentOperationState.RequiresAction,
            PaymentOperationState.Processing => Proto.PaymentOperationState.Processing,
            PaymentOperationState.Authorized => Proto.PaymentOperationState.Authorized,
            PaymentOperationState.Succeeded => Proto.PaymentOperationState.Succeeded,
            PaymentOperationState.Canceled => Proto.PaymentOperationState.Canceled,
            PaymentOperationState.Failed => Proto.PaymentOperationState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    extension(PaymentOperationTerminalDisposition disposition)
    {
        private Proto.PaymentOperationTerminalDisposition ToProto() => disposition switch
        {
            PaymentOperationTerminalDisposition.NonTerminal => Proto.PaymentOperationTerminalDisposition.NonTerminal,
            PaymentOperationTerminalDisposition.AttemptTerminal => Proto.PaymentOperationTerminalDisposition.AttemptTerminal,
            PaymentOperationTerminalDisposition.OperationTerminal => Proto.PaymentOperationTerminalDisposition.OperationTerminal,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };
    }

    extension(PaymentOperationRetryDisposition disposition)
    {
        private Proto.PaymentOperationRetryDisposition ToProto() => disposition switch
        {
            PaymentOperationRetryDisposition.ContinueCurrentAttempt => Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt,
            PaymentOperationRetryDisposition.RetryCurrentAttempt => Proto.PaymentOperationRetryDisposition.RetryCurrentAttempt,
            PaymentOperationRetryDisposition.CreateNewAttempt => Proto.PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationRetryDisposition.CreateNewOperation => Proto.PaymentOperationRetryDisposition.CreateNewOperation,
            PaymentOperationRetryDisposition.Reconcile => Proto.PaymentOperationRetryDisposition.Reconcile,
            PaymentOperationRetryDisposition.NotRetryable => Proto.PaymentOperationRetryDisposition.NotRetryable,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
        };
    }

    extension(PaymentOperationFailureCode code)
    {
        private Proto.PaymentOperationFailureCode ToProto() => code switch
        {
            PaymentOperationFailureCode.PaymentMethodRequired => Proto.PaymentOperationFailureCode.PaymentMethodRequired,
            PaymentOperationFailureCode.AuthenticationRequired => Proto.PaymentOperationFailureCode.AuthenticationRequired,
            PaymentOperationFailureCode.Declined => Proto.PaymentOperationFailureCode.Declined,
            PaymentOperationFailureCode.Expired => Proto.PaymentOperationFailureCode.Expired,
            PaymentOperationFailureCode.Canceled => Proto.PaymentOperationFailureCode.Canceled,
            PaymentOperationFailureCode.OperationConflict => Proto.PaymentOperationFailureCode.OperationConflict,
            PaymentOperationFailureCode.ProviderUnavailable => Proto.PaymentOperationFailureCode.ProviderUnavailable,
            PaymentOperationFailureCode.Unknown => Proto.PaymentOperationFailureCode.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };
    }

    private static T Invalid<T>(string fieldName, object value) =>
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"{fieldName} '{value}' is invalid."));
}
