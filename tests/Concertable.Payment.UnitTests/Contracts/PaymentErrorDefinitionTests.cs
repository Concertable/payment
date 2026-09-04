using Reunion.Errors;
using Concertable.Payment.Application.Errors;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentErrorDefinitionTests
{
    public static TheoryData<PaymentOperationFailureCode, PaymentOperationError> OperationFailures => new()
    {
        { PaymentOperationFailureCode.PaymentMethodRequired, new PaymentOperationError.PaymentMethodRequired() },
        { PaymentOperationFailureCode.AuthenticationRequired, new PaymentOperationError.AuthenticationRequired() },
        { PaymentOperationFailureCode.Declined, new PaymentOperationError.Declined() },
        { PaymentOperationFailureCode.Expired, new PaymentOperationError.Expired() },
        { PaymentOperationFailureCode.Canceled, new PaymentOperationError.Canceled() },
        { PaymentOperationFailureCode.OperationConflict, new PaymentOperationError.OperationConflict() },
        { PaymentOperationFailureCode.ProviderUnavailable, new PaymentOperationError.ProviderUnavailable() },
        { PaymentOperationFailureCode.Unknown, new PaymentOperationError.Unknown() }
    };

    public static TheoryData<IError, string, string, ErrorKind> Cases => new()
    {
        { new PaymentError.PayerNotFound(), "payment.payer_not_found", "The payer account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayeeNotFound(), "payment.payee_not_found", "The payee account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayerUnavailable(), "payment.payer_unavailable", "The payer account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.RecipientUnavailable(), "payment.recipient_unavailable", "The recipient account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.PaymentRejected(), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new PaymentError.CommissionFailure(new CommissionError.PricingChanged()), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new PaymentOperationError.PaymentMethodRequired(), "payment.operation.payment_method_required", "A usable payment method is required.", ErrorKind.PaymentRequired },
        { new PaymentOperationError.AuthenticationRequired(), "payment.operation.authentication_required", "Payment authentication is required.", ErrorKind.PaymentRequired },
        { new PaymentOperationError.Declined(), "payment.operation.declined", "The payment was declined.", ErrorKind.PaymentRequired },
        { new PaymentOperationError.Expired(), "payment.operation.expired", "The payment attempt expired.", ErrorKind.Conflict },
        { new PaymentOperationError.Canceled(), "payment.operation.canceled", "The payment operation was canceled.", ErrorKind.Conflict },
        { new PaymentOperationError.OperationConflict(), "payment.operation.conflict", "The operation identity conflicts with an existing payment operation.", ErrorKind.Conflict },
        { new PaymentOperationError.ProviderUnavailable(), "payment.operation.provider_unavailable", "The payment provider state is temporarily unavailable.", ErrorKind.Conflict },
        { new PaymentOperationError.Unknown(), "payment.operation.unknown", "The payment state could not be safely classified.", ErrorKind.Conflict },
        { new CommissionError.CurrencyMismatch(), "payment.commission_currency_mismatch", "The commission currency does not match this payment.", ErrorKind.Invalid },
        { new CommissionError.PricingChanged(), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new CommissionError.BindingNotFound(), "payment.commission_binding_not_found", "The commission binding was not found.", ErrorKind.NotFound },
        { new CommissionError.BindingMismatch(), "payment.commission_binding_mismatch", "The commission binding does not match this payment.", ErrorKind.Invalid },
        { new CommissionError.BindingIntentMismatch(), "payment.commission_intent_mismatch", "The commission binding does not match the payment intent.", ErrorKind.Invalid },
        { new CommissionError.GrossNotConfirmed(), "payment.commission_gross_not_confirmed", "The commission gross has not been confirmed.", ErrorKind.Conflict },
        { new CommissionError.GrossMismatch(), "payment.commission_gross_mismatch", "The commission gross does not match the confirmed amount.", ErrorKind.Conflict },
        { new ManagerPaymentError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new ManagerPaymentError.CommissionFailure(new CommissionError.PricingChanged()), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new ManagerPaymentOperationError.ManagerFailure(new ManagerPaymentError.PaymentFailure(new PaymentError.PaymentRejected())), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new ManagerPaymentOperationError.OperationConflict(), "payment.manager_operation_conflict", "The operation identity conflicts with an existing manager payment.", ErrorKind.Conflict },
        { new PaymentMethodChargeError.PaymentMethodFailure(new PaymentOperationError.PaymentMethodRequired()), "payment.operation.payment_method_required", "A usable payment method is required.", ErrorKind.PaymentRequired },
        { new PaymentMethodChargeError.ChargeFailure(new ManagerPaymentOperationError.OperationConflict()), "payment.manager_operation_conflict", "The operation identity conflicts with an existing manager payment.", ErrorKind.Conflict },
        { new PaymentMethodChargeError.AuthenticationRequired(), "payment.charge.authentication_required", "The committed payment method requires the payer to authenticate on-session before the charge can complete.", ErrorKind.PaymentRequired },
        { new EscrowDepositError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowDepositError.CommissionFailure(new CommissionError.PricingChanged()), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new EscrowCaptureError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowCaptureError.CommissionFailure(new CommissionError.PricingChanged()), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new EscrowReleaseError.EscrowNotFound(), "escrow.release_not_found", "Escrow not found.", ErrorKind.NotFound },
        { new EscrowReleaseError.EscrowNotHeld(), "escrow.release_not_held", "Only held escrow can be released.", ErrorKind.Conflict },
        { new EscrowReleaseOperationError.ReleaseFailure(new EscrowReleaseError.EscrowNotHeld()), "escrow.release_not_held", "Only held escrow can be released.", ErrorKind.Conflict },
        { new EscrowReleaseOperationError.OperationConflict(), "escrow.release_operation_conflict", "The operation identity conflicts with the escrow release.", ErrorKind.Conflict },
        { new EscrowReleaseError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowRefundError.EscrowNotFound(), "escrow.refund_not_found", "Escrow not found.", ErrorKind.NotFound },
        { new EscrowRefundError.EscrowNotRefundable(), "escrow.refund_not_allowed", "Escrow cannot be refunded in its current state.", ErrorKind.Conflict },
        { new EscrowRefundError.CommissionBindingNotFound(), "escrow.refund_commission_binding_not_found", "Commission binding not found.", ErrorKind.NotFound },
        { new EscrowRefundError.CurrencyMismatch(), "escrow.refund_currency_mismatch", "Refund currency does not match.", ErrorKind.Invalid },
        { new EscrowRefundError.AmountMustBePositive(), "escrow.refund_amount_invalid", "Refund amount must be positive.", ErrorKind.Invalid },
        { new EscrowRefundError.AmountExceedsRemaining(), "escrow.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount.", ErrorKind.Conflict },
        { new EscrowRefundError.Conflict(), "escrow.refund_conflict", "Another refund changed the refundable amount.", ErrorKind.Conflict },
        { new EscrowRefundError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new SettlementRefundError.SettlementNotFound(), "settlement.refund_not_found", "Settlement not found.", ErrorKind.NotFound },
        { new SettlementRefundError.SettlementNotRefundable(), "settlement.refund_not_allowed", "Settlement cannot be refunded in its current state.", ErrorKind.Conflict },
        { new SettlementRefundError.CommissionBindingNotFound(), "settlement.refund_commission_binding_not_found", "Commission binding not found.", ErrorKind.NotFound },
        { new SettlementRefundError.CurrencyMismatch(), "settlement.refund_currency_mismatch", "Refund currency does not match.", ErrorKind.Invalid },
        { new SettlementRefundError.AmountMustBePositive(), "settlement.refund_amount_invalid", "Refund amount must be positive.", ErrorKind.Invalid },
        { new SettlementRefundError.AmountExceedsRemaining(), "settlement.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount.", ErrorKind.Conflict },
        { new SettlementRefundError.Conflict(), "settlement.refund_conflict", "Another refund changed the refundable amount.", ErrorKind.Conflict },
        { new SettlementRefundError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new HoldSessionError.PaymentFailure(new PaymentError.PayerNotFound()), "payment.payer_not_found", "The payer account was not found.", ErrorKind.NotFound },
        { new HoldSessionError.CommissionFailure(new CommissionError.BindingMismatch()), "payment.commission_binding_mismatch", "The commission binding does not match this payment.", ErrorKind.Invalid }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Definition_ReturnsPublishedContract(
        IError error,
        string expectedCode,
        string expectedMessage,
        ErrorKind expectedKind)
    {
        var definition = error.Definition;

        Assert.Equal(expectedCode, definition.Code);
        Assert.Equal(expectedMessage, definition.Message);
        Assert.Equal(expectedKind, definition.Kind);
    }

    [Theory]
    [MemberData(nameof(OperationFailures))]
    public void ErrorFromCode_KnownFailure_ReturnsTypedError(
        PaymentOperationFailureCode code,
        PaymentOperationError expected) =>
        Assert.Equal(expected, PaymentOperationError.FromCode(code));

    [Theory]
    [MemberData(nameof(OperationFailures))]
    public void FailureFromCode_KnownFailure_UsesErrorDefinition(
        PaymentOperationFailureCode code,
        PaymentOperationError error) =>
        Assert.Equal(
            new PaymentOperationFailure(code, error.Definition.Message),
            PaymentOperationFailure.FromCode(code));

    [Fact]
    public void ErrorFromCode_UnknownFailure_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaymentOperationError.FromCode((PaymentOperationFailureCode)999));

    [Fact]
    public void FailureFromCode_UnknownFailure_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaymentOperationFailure.FromCode((PaymentOperationFailureCode)999));
}
