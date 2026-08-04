using Concertable.Kernel.Errors;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Grpc;
using Concertable.Payment.Infrastructure.Grpc;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentErrorTests
{
    public static TheoryData<IError, string, string, ErrorKind> Definitions => new()
    {
        { CommissionError.BindingNotFound, "payment.commission_binding_not_found", "The commission binding was not found.", ErrorKind.NotFound },
        { CommissionError.BindingMismatch, "payment.commission_binding_mismatch", "The commission binding does not match this payment.", ErrorKind.Invalid },
        { CommissionError.CurrencyMismatch, "payment.commission_currency_mismatch", "The commission currency does not match this payment.", ErrorKind.Invalid },
        { CommissionError.BindingIntentMismatch, "payment.commission_intent_mismatch", "The commission binding does not match the payment intent.", ErrorKind.Invalid },
        { CommissionError.PricingChanged, "payment.commission_pricing_changed", "The commission pricing has changed.", ErrorKind.Conflict },
        { CommissionError.ExpectedAmountsInvalid, "payment.commission_expected_amounts_invalid", "The expected commission amounts are invalid.", ErrorKind.Invalid },
        { new PaymentError.PayerNotFound(), "payment.payer_not_found", "The payer account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayeeNotFound(), "payment.payee_not_found", "The payee account was not found.", ErrorKind.NotFound },
        { new PaymentError.PayerUnavailable(), "payment.payer_unavailable", "The payer account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.RecipientUnavailable(), "payment.recipient_unavailable", "The recipient account is not ready for payments.", ErrorKind.Conflict },
        { new PaymentError.PaymentRejected(), "payment.rejected", "The payment was rejected.", ErrorKind.PaymentRequired },
        { ReleaseError.EscrowNotFound, "payment.escrow_not_found", "The escrow payment was not found.", ErrorKind.NotFound },
        { ReleaseError.InvalidEscrowState, "payment.escrow_release_invalid_state", "The escrow payment cannot be released in its current state.", ErrorKind.Conflict },
        { ReleaseError.RecipientUnavailable, "payment.recipient_unavailable", "The recipient account is not ready for payments.", ErrorKind.Conflict },
        { ReleaseError.ReleaseRejected, "payment.escrow_release_rejected", "The escrow release was rejected.", ErrorKind.Invalid },
        { RefundError.EscrowNotFound, "payment.escrow_not_found", "The escrow payment was not found.", ErrorKind.NotFound },
        { RefundError.InvalidEscrowState, "payment.escrow_refund_invalid_state", "The escrow payment cannot be refunded in its current state.", ErrorKind.Conflict },
        { RefundError.CurrencyMismatch, "payment.refund_currency_mismatch", "The refund currency does not match the payment.", ErrorKind.Invalid },
        { RefundError.InvalidAmount, "payment.refund_amount_invalid", "The refund amount is invalid.", ErrorKind.Invalid },
        { RefundError.CommissionBindingNotFound, "payment.commission_binding_not_found", "The commission binding was not found.", ErrorKind.NotFound },
        { RefundError.RefundRejected, "payment.refund_rejected", "The refund was rejected.", ErrorKind.Invalid }
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    public void Definition_ReturnsStableContract(IError error, string expectedCode, string expectedMessage, ErrorKind expectedKind)
    {
        var definition = error.Definition;

        Assert.Equal(expectedCode, definition.Code);
        Assert.Equal(expectedMessage, definition.Message);
        Assert.Equal(expectedKind, definition.Kind);
    }

    [Fact]
    public void CompositeErrors_ForwardDependencyDefinition()
    {
        var commission = CommissionError.PricingChanged;
        var payment = new PaymentError.CommissionFailure(commission);

        Assert.Equal(commission.Definition, payment.Definition);
        Assert.Equal(payment.Definition, new DepositError.PaymentFailure(payment).Definition);
        Assert.Equal(commission.Definition, new DepositError.CommissionFailure(commission).Definition);
        Assert.Equal(payment.Definition, new CaptureError.PaymentFailure(payment).Definition);
        Assert.Equal(commission.Definition, new CaptureError.CommissionFailure(commission).Definition);
    }

    [Fact]
    public void ToRpcException_PreservesLegacyMessageAndStructuredDetail()
    {
        var error = new PaymentError.PaymentRejected();

        var exception = error.ToRpcException();

        Assert.Equal(global::Grpc.Core.StatusCode.FailedPrecondition, exception.StatusCode);
        Assert.Equal(error.Definition.Message, exception.Status.Detail);
        var detail = OperationErrorDetail.Parser.ParseFrom(Assert.Single(exception.Trailers).ValueBytes);
        Assert.Equal(error.Definition.Code, detail.Code);
        Assert.Equal(error.Definition.Message, detail.Message);
    }
}
