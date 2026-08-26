extern alias PaymentClient;

using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentRequestTests
{
    [Fact]
    public void ManagerPay_Create_WithOperationId_MapsRequest()
    {
        var operationId = Guid.CreateVersion7();
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();

        var request = Proto.ManagerPayRequest.Create(
            operationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            "pm_test",
            PaymentSession.OffSession,
            42);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(payerId.ToString("D"), request.PayerId);
        Assert.Equal(payeeId.ToString("D"), request.PayeeId);
        Assert.Equal(5000, request.Amount.AmountMinor);
        Assert.Equal("pm_test", request.PaymentMethodId);
        Assert.Equal(Proto.PaymentSessionType.OffSession, request.Session);
        Assert.Equal(42, request.BookingId);
    }

    [Fact]
    public void ManagerPay_Create_EmptyOperationId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.ManagerPayRequest.Create(
            Guid.Empty,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            "pm_test",
            PaymentSession.OnSession,
            42));
    }

    [Fact]
    public void ReleaseByBookingId_Create_WithOperationId_MapsRequest()
    {
        var operationId = Guid.CreateVersion7();

        var request = Proto.ReleaseByBookingIdRequest.Create(operationId, 42);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(42, request.BookingId);
    }

    [Fact]
    public void ReleaseByBookingId_Create_EmptyOperationId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Proto.ReleaseByBookingIdRequest.Create(Guid.Empty, 42));
    }
}
