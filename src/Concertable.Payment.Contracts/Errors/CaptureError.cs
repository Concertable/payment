using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record CaptureError : IError
{
    partial record PaymentFailure(PaymentError Error);
    partial record CommissionFailure(CommissionError Error);

    public static CaptureError Payment(PaymentError error) => new PaymentFailure(error);
    public static CaptureError Commission(CommissionError error) => new CommissionFailure(error);

    public ErrorDefinition Definition => Match(
        paymentFailure => paymentFailure.Error.Definition,
        commissionFailure => commissionFailure.Error.Definition);
}
