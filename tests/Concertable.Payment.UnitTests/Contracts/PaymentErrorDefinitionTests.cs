using Concertable.Kernel.Errors;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentErrorDefinitionTests
{
    public static TheoryData<IError, string, string, ErrorKind> Cases => new()
    {
        { PaymentError.PayerNotFound(), "payment.payer_not_found", "Payer payment account not found.", ErrorKind.NotFound },
        { PaymentError.PayeeNotFound(), "payment.payee_not_found", "Payee payment account not found.", ErrorKind.NotFound },
        { PaymentError.PayerNotConfigured(), "payment.payer_not_configured", "Payer payment account is not configured.", ErrorKind.Invalid },
        { PaymentError.PayeeNotConfigured(), "payment.payee_not_configured", "Payee payment account is not configured.", ErrorKind.Invalid },
        { PaymentError.PayeePayoutsUnavailable(), "payment.payee_payouts_unavailable", "Payee is not eligible for payouts.", ErrorKind.Invalid },
        { PaymentError.Declined(), "payment.declined", "The payment was declined.", ErrorKind.PaymentRequired },
        { PaymentError.Rejected(), "payment.rejected", "The payment provider rejected the operation.", ErrorKind.PaymentRequired },
        { CommissionError.CurrencyMismatch(), "commission.currency_mismatch", "Commission currency does not match.", ErrorKind.Invalid },
        { CommissionError.PricingChanged(), "commission.pricing_changed", "Commission pricing has changed.", ErrorKind.Conflict },
        { CommissionError.BindingNotFound(), "commission.binding_not_found", "Commission binding not found.", ErrorKind.NotFound },
        { CommissionError.BindingMismatch(), "commission.binding_mismatch", "Commission binding does not match the operation.", ErrorKind.Conflict },
        { CommissionError.BindingIntentMismatch(), "commission.binding_intent_mismatch", "Commission binding does not match the payment intent.", ErrorKind.Conflict },
        { CommissionError.ExpectedAmountsInvalid(), "commission.expected_amounts_invalid", "Expected commission amounts are invalid.", ErrorKind.Invalid },
        { ManagerPaymentError.Payment(PaymentError.Declined()), "payment.declined", "The payment was declined.", ErrorKind.PaymentRequired },
        { ManagerPaymentError.Commission(CommissionError.PricingChanged()), "commission.pricing_changed", "Commission pricing has changed.", ErrorKind.Conflict },
        { EscrowDepositError.Payment(PaymentError.Declined()), "payment.declined", "The payment was declined.", ErrorKind.PaymentRequired },
        { EscrowDepositError.Commission(CommissionError.PricingChanged()), "commission.pricing_changed", "Commission pricing has changed.", ErrorKind.Conflict },
        { EscrowCaptureError.Payment(PaymentError.Declined()), "payment.declined", "The payment was declined.", ErrorKind.PaymentRequired },
        { EscrowCaptureError.Commission(CommissionError.PricingChanged()), "commission.pricing_changed", "Commission pricing has changed.", ErrorKind.Conflict },
        { EscrowReleaseError.EscrowNotFound(), "escrow.release_not_found", "Escrow not found.", ErrorKind.NotFound },
        { EscrowReleaseError.EscrowNotHeld(), "escrow.release_not_held", "Only held escrow can be released.", ErrorKind.Conflict },
        { EscrowReleaseError.Payment(PaymentError.Rejected()), "payment.rejected", "The payment provider rejected the operation.", ErrorKind.PaymentRequired },
        { EscrowRefundError.EscrowNotFound(), "escrow.refund_not_found", "Escrow not found.", ErrorKind.NotFound },
        { EscrowRefundError.EscrowNotRefundable(), "escrow.refund_not_allowed", "Escrow cannot be refunded in its current state.", ErrorKind.Conflict },
        { EscrowRefundError.CommissionBindingNotFound(), "escrow.refund_commission_binding_not_found", "Commission binding not found.", ErrorKind.NotFound },
        { EscrowRefundError.CurrencyMismatch(), "escrow.refund_currency_mismatch", "Refund currency does not match.", ErrorKind.Invalid },
        { EscrowRefundError.AmountMustBePositive(), "escrow.refund_amount_invalid", "Refund amount must be positive.", ErrorKind.Invalid },
        { EscrowRefundError.AmountExceedsRemaining(), "escrow.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount.", ErrorKind.Conflict },
        { EscrowRefundError.Conflict(), "escrow.refund_conflict", "Another refund changed the refundable amount.", ErrorKind.Conflict },
        { EscrowRefundError.Payment(PaymentError.Rejected()), "payment.rejected", "The payment provider rejected the operation.", ErrorKind.PaymentRequired },
        { HoldSessionError.Payment(PaymentError.PayerNotFound()), "payment.payer_not_found", "Payer payment account not found.", ErrorKind.NotFound },
        { HoldSessionError.Commission(CommissionError.BindingMismatch()), "commission.binding_mismatch", "Commission binding does not match the operation.", ErrorKind.Conflict }
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
}
