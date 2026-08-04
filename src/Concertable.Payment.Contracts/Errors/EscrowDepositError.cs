using Concertable.Kernel.Errors;
using Concertable.Kernel.Functional;
using Dunet;

namespace Concertable.Payment.Contracts.Errors;

[Union(EnableImplicitConversions = false)]
public abstract partial record EscrowDepositError : IError
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

    public static Option<EscrowDepositError> FromCode(string code) =>
        PaymentError.FromCode(code).Match(
            payment => Option.Some<EscrowDepositError>(new PaymentFailure(payment)),
            () => CommissionError.FromCode(code).Match(
                commission => Option.Some<EscrowDepositError>(new CommissionFailure(commission)),
                Option.None<EscrowDepositError>));
}
