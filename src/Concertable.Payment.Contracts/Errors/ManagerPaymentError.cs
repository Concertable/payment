using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record ManagerPaymentError : IError
{
    public abstract ErrorDefinition Definition { get; }

    public partial record PaymentFailure(PaymentError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public partial record CommissionFailure(CommissionError Error)
    {
        public override ErrorDefinition Definition => Error.Definition;
    }

    public static Option<ManagerPaymentError> FromCode(string code) =>
        CommissionError.FromCode(code).Match(
            commission => Option.Some<ManagerPaymentError>(new CommissionFailure(commission)),
            () => PaymentError.FromCode(code).Match(
                payment => Option.Some<ManagerPaymentError>(new PaymentFailure(payment)),
                Option.None<ManagerPaymentError>));
}
