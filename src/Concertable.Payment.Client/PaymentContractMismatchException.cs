using Grpc.Core;

namespace Concertable.Payment.Client;

public sealed class PaymentContractMismatchException : Exception
{
    public PaymentContractMismatchException(string code, RpcException innerException)
        : base($"The Payment client does not recognize operation error contract '{code}'.", innerException) { }
}
