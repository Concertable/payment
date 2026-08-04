using Concertable.Kernel.Errors;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union]
public partial record DepositError : IError
{
    partial record PaymentFailure(PaymentError Error);
    partial record CommissionFailure(CommissionError Error);

    public static DepositError Payment(PaymentError error) => new PaymentFailure(error);
    public static DepositError Commission(CommissionError error) => new CommissionFailure(error);

    public ErrorDefinition Definition => Match(
        paymentFailure => paymentFailure.Error.Definition,
        commissionFailure => commissionFailure.Error.Definition);
}
