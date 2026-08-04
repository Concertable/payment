using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Grpc;
using Concertable.Payment.Infrastructure.Grpc;

namespace Concertable.Payment.UnitTests.Contracts;

public sealed class PaymentErrorTests
{
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
