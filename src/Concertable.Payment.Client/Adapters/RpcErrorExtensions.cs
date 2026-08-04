using Concertable.Payment.Contracts.Errors;
using Grpc.Core;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.Client.Adapters;

internal static class RpcErrorExtensions
{
    private const string ErrorTrailer = "payment-error-bin";

    public static Proto.OperationErrorDetail? GetOperationError(this RpcException exception)
    {
        var bytes = exception.Trailers
            .FirstOrDefault(entry => entry.Key == ErrorTrailer)
            ?.ValueBytes;
        return bytes is null
            ? null
            : Proto.OperationErrorDetail.Parser.ParseFrom(bytes);
    }

    public static CommissionError ToCommissionError(this RpcException exception) =>
        exception.GetOperationError()?.Code switch
        {
            "payment.commission_binding_not_found" => CommissionError.BindingNotFound,
            "payment.commission_binding_mismatch" => CommissionError.BindingMismatch,
            "payment.commission_currency_mismatch" => CommissionError.CurrencyMismatch,
            "payment.commission_intent_mismatch" => CommissionError.BindingIntentMismatch,
            "payment.commission_pricing_changed" => CommissionError.PricingChanged,
            "payment.commission_expected_amounts_invalid" => CommissionError.ExpectedAmountsInvalid,
            _ => CommissionError.BindingMismatch
        };

    public static PaymentError ToPaymentError(this RpcException exception) =>
        exception.GetOperationError()?.Code switch
        {
            "payment.payer_not_found" => new PaymentError.PayerNotFound(),
            "payment.payee_not_found" => new PaymentError.PayeeNotFound(),
            "payment.payer_unavailable" => new PaymentError.PayerUnavailable(),
            "payment.recipient_unavailable" => new PaymentError.RecipientUnavailable(),
            "payment.commission_binding_not_found" or
            "payment.commission_binding_mismatch" or
            "payment.commission_currency_mismatch" or
            "payment.commission_intent_mismatch" or
            "payment.commission_pricing_changed" or
            "payment.commission_expected_amounts_invalid" => new PaymentError.CommissionFailure(exception.ToCommissionError()),
            _ => new PaymentError.PaymentRejected()
        };

    public static DepositError ToDepositError(this RpcException exception) =>
        exception.GetOperationError()?.Code?.StartsWith("payment.commission_", StringComparison.Ordinal) == true
            ? new DepositError.CommissionFailure(exception.ToCommissionError())
            : new DepositError.PaymentFailure(exception.ToPaymentError());

    public static CaptureError ToCaptureError(this RpcException exception) =>
        exception.GetOperationError()?.Code?.StartsWith("payment.commission_", StringComparison.Ordinal) == true
            ? new CaptureError.CommissionFailure(exception.ToCommissionError())
            : new CaptureError.PaymentFailure(exception.ToPaymentError());

    public static ReleaseError ToReleaseError(this RpcException exception) =>
        exception.GetOperationError()?.Code switch
        {
            "payment.escrow_not_found" => ReleaseError.EscrowNotFound,
            "payment.escrow_release_invalid_state" => ReleaseError.InvalidEscrowState,
            "payment.recipient_unavailable" => ReleaseError.RecipientUnavailable,
            _ => ReleaseError.ReleaseRejected
        };

    public static RefundError ToRefundError(this RpcException exception) =>
        exception.GetOperationError()?.Code switch
        {
            "payment.escrow_not_found" => RefundError.EscrowNotFound,
            "payment.escrow_refund_invalid_state" => RefundError.InvalidEscrowState,
            "payment.refund_currency_mismatch" => RefundError.CurrencyMismatch,
            "payment.refund_amount_invalid" => RefundError.InvalidAmount,
            "payment.commission_binding_not_found" => RefundError.CommissionBindingNotFound,
            _ => RefundError.RefundRejected
        };
}
