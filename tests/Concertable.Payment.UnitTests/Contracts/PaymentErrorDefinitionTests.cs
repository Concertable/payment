using Concertable.Kernel.Errors;
using Concertable.Payment.Contracts.Errors;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentErrorDefinitionTests
{
    public static TheoryData<IError, string, string, ErrorKind> Cases => new()
    {
        { new PaymentError.PayerNotFound(), "payment.payer_not_found", "The payer account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayeeNotFound(), "payment.payee_not_found", "The payee account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayerUnavailable(), "payment.payer_unavailable", "The payer account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.RecipientUnavailable(), "payment.recipient_unavailable", "The recipient account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.PaymentRejected(), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new PaymentError.CommissionFailure(CommissionError.PricingChanged), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { CommissionError.CurrencyMismatch, "payment.commission_currency_mismatch", "The commission currency does not match this payment.", ErrorKind.Invalid },
        { CommissionError.PricingChanged, "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { CommissionError.BindingNotFound, "payment.commission_binding_not_found", "The commission binding was not found.", ErrorKind.NotFound },
        { CommissionError.BindingMismatch, "payment.commission_binding_mismatch", "The commission binding does not match this payment.", ErrorKind.Invalid },
        { CommissionError.BindingIntentMismatch, "payment.commission_intent_mismatch", "The commission binding does not match the payment intent.", ErrorKind.Invalid },
        { CommissionError.ExpectedAmountsInvalid, "payment.commission_expected_amounts_invalid", "The expected commission amounts are invalid.", ErrorKind.Invalid },
        { new ManagerPaymentError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new ManagerPaymentError.CommissionFailure(CommissionError.PricingChanged), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new EscrowDepositError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowDepositError.CommissionFailure(CommissionError.PricingChanged), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new EscrowCaptureError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowCaptureError.CommissionFailure(CommissionError.PricingChanged), "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { new EscrowReleaseError.EscrowNotFound(), "escrow.release_not_found", "Escrow not found.", ErrorKind.NotFound },
        { new EscrowReleaseError.EscrowNotHeld(), "escrow.release_not_held", "Only held escrow can be released.", ErrorKind.Conflict },
        { new EscrowReleaseError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new EscrowRefundError.EscrowNotFound(), "escrow.refund_not_found", "Escrow not found.", ErrorKind.NotFound },
        { new EscrowRefundError.EscrowNotRefundable(), "escrow.refund_not_allowed", "Escrow cannot be refunded in its current state.", ErrorKind.Conflict },
        { new EscrowRefundError.CommissionBindingNotFound(), "escrow.refund_commission_binding_not_found", "Commission binding not found.", ErrorKind.NotFound },
        { new EscrowRefundError.CurrencyMismatch(), "escrow.refund_currency_mismatch", "Refund currency does not match.", ErrorKind.Invalid },
        { new EscrowRefundError.AmountMustBePositive(), "escrow.refund_amount_invalid", "Refund amount must be positive.", ErrorKind.Invalid },
        { new EscrowRefundError.AmountExceedsRemaining(), "escrow.refund_amount_exceeds_remaining", "Refund amount exceeds the remaining refundable amount.", ErrorKind.Conflict },
        { new EscrowRefundError.Conflict(), "escrow.refund_conflict", "Another refund changed the refundable amount.", ErrorKind.Conflict },
        { new EscrowRefundError.PaymentFailure(new PaymentError.PaymentRejected()), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { new HoldSessionError.PaymentFailure(new PaymentError.PayerNotFound()), "payment.payer_not_found", "The payer account was not found.", ErrorKind.NotFound },
        { new HoldSessionError.CommissionFailure(CommissionError.BindingMismatch), "payment.commission_binding_mismatch", "The commission binding does not match this payment.", ErrorKind.Invalid }
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
