using System.Collections.Frozen;
using Reunion.Errors;
using Concertable.Payment.Contracts;
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
        Index(Enum.GetValues<PaymentOperationFailureCode>().Select(PaymentOperationError.FromCode));

    private static readonly FrozenDictionary<Proto.OperationErrorKind, ErrorKind> operationErrorKinds =
        new Dictionary<Proto.OperationErrorKind, ErrorKind>
        {
            [Proto.OperationErrorKind.OperationErrorInvalid] = ErrorKind.Invalid,
            [Proto.OperationErrorKind.OperationErrorNotFound] = ErrorKind.NotFound,
            [Proto.OperationErrorKind.OperationErrorConflict] = ErrorKind.Conflict,
            [Proto.OperationErrorKind.OperationErrorUnauthenticated] = ErrorKind.Unauthenticated,
            [Proto.OperationErrorKind.OperationErrorForbidden] = ErrorKind.Forbidden,
            [Proto.OperationErrorKind.OperationErrorPaymentRequired] = ErrorKind.PaymentRequired
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, PaymentMethodChargeError> paymentMethodChargeErrors =
        Index(paymentOperationErrors.Values.Select(error =>
                (PaymentMethodChargeError)new PaymentMethodChargeError.PaymentMethodFailure(error))
            .Concat(directPaymentErrors.Select(error =>
                (PaymentMethodChargeError)new PaymentMethodChargeError.PaymentFailure(error)))
            .Concat(commissionErrors.Values.Select(error =>
                (PaymentMethodChargeError)new PaymentMethodChargeError.CommissionFailure(error)))
            .Append(new PaymentMethodChargeError.AuthenticationRequired())
            .Append(new PaymentMethodChargeError.OperationConflict()));

    private static readonly FrozenDictionary<string, EscrowDepositError> escrowDepositErrors =
        Index(Composite<EscrowDepositError>(
            error => new EscrowDepositError.PaymentFailure(error),
            error => new EscrowDepositError.CommissionFailure(error))
            .Concat(paymentOperationErrors.Values.Select(error =>
                (EscrowDepositError)new EscrowDepositError.PaymentOperationFailure(error))));

    private static readonly FrozenDictionary<string, EscrowCaptureError> escrowCaptureErrors =
        Index(Composite<EscrowCaptureError>(
            error => new EscrowCaptureError.PaymentFailure(error),
            error => new EscrowCaptureError.CommissionFailure(error))
            .Concat(paymentOperationErrors.Values.Select(error =>
                (EscrowCaptureError)new EscrowCaptureError.PaymentOperationFailure(error))));

    private static readonly FrozenDictionary<string, EscrowReleaseError> escrowReleaseErrors =
        Index(new EscrowReleaseError[]
        {
            new EscrowReleaseError.EscrowNotFound(),
            new EscrowReleaseError.EscrowNotHeld()
        }.Concat(paymentErrors.Values.Select(error =>
            (EscrowReleaseError)new EscrowReleaseError.PaymentFailure(error))));

    private static readonly FrozenDictionary<string, EscrowReleaseOperationError> escrowReleaseOperationErrors =
        Index(new EscrowReleaseOperationError[]
        {
            new EscrowReleaseOperationError.OperationConflict()
        }.Concat(escrowReleaseErrors.Values.Select(error =>
            (EscrowReleaseOperationError)new EscrowReleaseOperationError.ReleaseFailure(error))));

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

    private static readonly FrozenDictionary<string, SettlementRefundError> settlementRefundErrors =
        Index(new SettlementRefundError[]
        {
            new SettlementRefundError.SettlementNotFound(),
            new SettlementRefundError.SettlementNotRefundable(),
            new SettlementRefundError.CommissionBindingNotFound(),
            new SettlementRefundError.CurrencyMismatch(),
            new SettlementRefundError.AmountMustBePositive(),
            new SettlementRefundError.AmountExceedsRemaining(),
            new SettlementRefundError.Conflict()
        }.Concat(paymentErrors.Values.Select(error =>
            (SettlementRefundError)new SettlementRefundError.PaymentFailure(error))));

    extension(RpcException exception)
    {
        internal bool HasOperationErrorDetail() =>
            exception.Trailers.Any(entry => entry.Key == TrailerKey && entry.IsBinary);

        internal CommissionError ToCommissionError() =>
            exception.ToError(commissionErrors);

        internal PaymentError ToPaymentError() =>
            exception.ToError(paymentErrors);

        internal PaymentOperationError ToPaymentOperationError() =>
            exception.ToError(paymentOperationErrors);

        internal PaymentMethodChargeError ToPaymentMethodChargeError() =>
            exception.ToError(paymentMethodChargeErrors);

        internal EscrowDepositError ToEscrowDepositError() =>
            exception.ToError(escrowDepositErrors);

        internal EscrowCaptureError ToEscrowCaptureError() =>
            exception.ToError(escrowCaptureErrors);

        internal EscrowReleaseError ToEscrowReleaseError() =>
            exception.ToError(escrowReleaseErrors);

        internal EscrowReleaseOperationError ToEscrowReleaseOperationError() =>
            exception.ToError(escrowReleaseOperationErrors);

        internal EscrowRefundError ToEscrowRefundError() =>
            exception.ToError(escrowRefundErrors);

        internal SettlementRefundError ToSettlementRefundError() =>
            exception.ToError(settlementRefundErrors);

        private TError ToError<TError>(FrozenDictionary<string, TError> errors)
            where TError : IError
        {
            var detail = exception.ToOperationErrorDetail();

            if (!errors.TryGetValue(detail.Code, out var error)
                || detail.Message != error.Definition.Message
                || detail.Kind.ToErrorKind() != error.Definition.Kind)
            {
                throw new PaymentContractMismatchException(detail.Code, exception);
            }

            return error;
        }

        private Proto.OperationErrorDetail ToOperationErrorDetail()
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
    }

    extension(Proto.OperationErrorKind kind)
    {
        private ErrorKind? ToErrorKind() =>
            operationErrorKinds.TryGetValue(kind, out var errorKind)
                ? errorKind
                : null;
    }

    private static FrozenDictionary<string, TError> Index<TError>(IEnumerable<TError> errors)
        where TError : IError =>
        errors.ToFrozenDictionary(error => error.Definition.Code);

    private static IEnumerable<TError> Composite<TError>(
        Func<PaymentError, TError> payment,
        Func<CommissionError, TError> commission) =>
        directPaymentErrors.Select(payment).Concat(commissionErrorCases.Select(commission));

}
