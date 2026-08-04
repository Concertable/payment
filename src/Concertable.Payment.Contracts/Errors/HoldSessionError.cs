using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record HoldSessionError : IError
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

    public static Option<HoldSessionError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<HoldSessionError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<HoldSessionError>(new CommissionFailure(commission)),
                Option.None<HoldSessionError>));
}
