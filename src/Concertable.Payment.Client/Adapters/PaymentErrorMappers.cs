using System.Collections.Frozen;
using Reunion.Errors;
using Concertable.Payment.Contracts.Errors;
using Google.Protobuf;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class PaymentErrorMappers
{
    private const string TrailerKey = "concertable-payment-error-bin";

    private static readonly PaymentError[] directPaymentErrors =
    [
        new PaymentError.PayerNotFound(),
        new PaymentError.PayeeNotFound(),
        new PaymentError.PayerUnavailable(),
        new PaymentError.RecipientUnavailable(),
        new PaymentError.PaymentRejected()
    ];

    private static readonly CommissionError[] commissionErrorCases =
    [
        new CommissionError.BindingNotFound(),
        new CommissionError.BindingMismatch(),
        new CommissionError.CurrencyMismatch(),
        new CommissionError.BindingIntentMismatch(),
        new CommissionError.PricingChanged(),
        new CommissionError.GrossNotConfirmed(),
        new CommissionError.GrossMismatch()
    ];

    private static readonly FrozenDictionary<string, CommissionError> commissionErrors =
        Index(commissionErrorCases);

    private static readonly FrozenDictionary<string, PaymentError> paymentErrors =
        Index(directPaymentErrors.Concat(
            commissionErrorCases.Select(error =>
                (PaymentError)new PaymentError.CommissionFailure(error))));

    private static readonly FrozenDictionary<string, PaymentOperationError> paymentOperationErrors =
        Index(new PaymentOperationError[]
        {
            new PaymentOperationError.PaymentMethodRequired(),
            new PaymentOperationError.AuthenticationRequired(),
            new PaymentOperationError.Declined(),
            new PaymentOperationError.Expired(),
            new PaymentOperationError.Canceled(),
            new PaymentOperationError.OperationConflict(),
            new PaymentOperationError.ProviderUnavailable(),
            new PaymentOperationError.Unknown()
        });

    private static readonly FrozenDictionary<string, ManagerPaymentError> managerPaymentErrors =
        Index(Composite<ManagerPaymentError>(
            error => new ManagerPaymentError.PaymentFailure(error),
            error => new ManagerPaymentError.CommissionFailure(error)));

    private static readonly FrozenDictionary<string, HoldSessionError> holdSessionErrors =
        Index(Composite<HoldSessionError>(
            error => new HoldSessionError.PaymentFailure(error),
            error => new HoldSessionError.CommissionFailure(error)));

    private static readonly FrozenDictionary<string, EscrowDepositError> escrowDepositErrors =
        Index(Composite<EscrowDepositError>(
            error => new EscrowDepositError.PaymentFailure(error),
            error => new EscrowDepositError.CommissionFailure(error)));

    private static readonly FrozenDictionary<string, EscrowCaptureError> escrowCaptureErrors =
        Index(Composite<EscrowCaptureError>(
            error => new EscrowCaptureError.PaymentFailure(error),
            error => new EscrowCaptureError.CommissionFailure(error)));

    private static readonly FrozenDictionary<string, EscrowReleaseError> escrowReleaseErrors =
        Index(new EscrowReleaseError[]
        {
            new EscrowReleaseError.EscrowNotFound(),
            new EscrowReleaseError.EscrowNotHeld()
        }.Concat(paymentErrors.Values.Select(error =>
            (EscrowReleaseError)new EscrowReleaseError.PaymentFailure(error))));

    private static readonly FrozenDictionary<string, EscrowRefundError> escrowRefundErrors =
        Index(new EscrowRefundError[]
        {
            new EscrowRefundError.EscrowNotFound(),
            new EscrowRefundError.EscrowNotRefundable(),
            new EscrowRefundError.CommissionBindingNotFound(),
            new EscrowRefundError.CurrencyMismatch(),
            new EscrowRefundError.AmountMustBePositive(),
            new EscrowRefundError.AmountExceedsRemaining(),
            new EscrowRefundError.Conflict()
        }.Concat(paymentErrors.Values.Select(error =>
            (EscrowRefundError)new EscrowRefundError.PaymentFailure(error))));

    internal static bool HasOperationErrorDetail(this RpcException exception) =>
        exception.Trailers.Any(entry => entry.Key == TrailerKey && entry.IsBinary);

    internal static CommissionError ToCommissionError(this RpcException exception) =>
        ToError(exception, commissionErrors);

    internal static PaymentError ToPaymentError(this RpcException exception) =>
        ToError(exception, paymentErrors);

    extension(RpcException exception)
    {
        internal PaymentOperationError ToPaymentOperationError() =>
            ToError(exception, paymentOperationErrors);
    }

    internal static ManagerPaymentError ToManagerPaymentError(this RpcException exception) =>
        ToError(exception, managerPaymentErrors);

    internal static HoldSessionError ToHoldSessionError(this RpcException exception) =>
        ToError(exception, holdSessionErrors);

    internal static EscrowDepositError ToEscrowDepositError(this RpcException exception) =>
        ToError(exception, escrowDepositErrors);

    internal static EscrowCaptureError ToEscrowCaptureError(this RpcException exception) =>
        ToError(exception, escrowCaptureErrors);

    internal static EscrowReleaseError ToEscrowReleaseError(this RpcException exception) =>
        ToError(exception, escrowReleaseErrors);

    internal static EscrowRefundError ToEscrowRefundError(this RpcException exception) =>
        ToError(exception, escrowRefundErrors);

    private static FrozenDictionary<string, TError> Index<TError>(IEnumerable<TError> errors)
        where TError : IError =>
        errors.ToFrozenDictionary(error => error.Definition.Code);

    private static IEnumerable<TError> Composite<TError>(
        Func<PaymentError, TError> payment,
        Func<CommissionError, TError> commission) =>
        directPaymentErrors.Select(payment).Concat(commissionErrorCases.Select(commission));

    private static TError ToError<TError>(
        RpcException exception,
        FrozenDictionary<string, TError> errors)
        where TError : IError
    {
        var detail = exception.ToOperationErrorDetail();

        if (!errors.TryGetValue(detail.Code, out var error)
            || detail.Message != error.Definition.Message
            || !detail.Kind.Matches(error.Definition.Kind))
        {
            throw new PaymentContractMismatchException(detail.Code, exception);
        }

        return error;
    }

    private static Proto.OperationErrorDetail ToOperationErrorDetail(this RpcException exception)
    {
        var entry = exception.Trailers.First(item => item.Key == TrailerKey && item.IsBinary);

        try
        {
            return Proto.OperationErrorDetail.Parser.ParseFrom(entry.ValueBytes);
        }
        catch (InvalidProtocolBufferException)
        {
            throw exception;
        }
    }

    private static bool Matches(this Proto.OperationErrorKind kind, ErrorKind expected) =>
        (kind, expected) switch
        {
            (Proto.OperationErrorKind.OperationErrorInvalid, ErrorKind.Invalid) => true,
            (Proto.OperationErrorKind.OperationErrorNotFound, ErrorKind.NotFound) => true,
            (Proto.OperationErrorKind.OperationErrorConflict, ErrorKind.Conflict) => true,
            (Proto.OperationErrorKind.OperationErrorUnauthenticated, ErrorKind.Unauthenticated) => true,
            (Proto.OperationErrorKind.OperationErrorForbidden, ErrorKind.Forbidden) => true,
            (Proto.OperationErrorKind.OperationErrorPaymentRequired, ErrorKind.PaymentRequired) => true,
            _ => false
        };
}
