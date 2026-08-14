using Concertable.Payment.Grpc;
using Concertable.Payment.Infrastructure.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class ManagerPaymentRequestMapperTests
{
    [Fact]
    public void ToCommand_WithValidPeriod_MapsPayeeAndRange()
    {
        var payeeId = Guid.NewGuid();
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var request = new PaymentPeriodRequest
        {
            PayeeId = payeeId.ToString(),
            PeriodStart = Timestamp.FromDateTime(start),
            PeriodEnd = Timestamp.FromDateTime(end)
        };

        var command = request.ToCommand();

        Assert.Equal(payeeId, command.PayeeId);
        Assert.Equal(start, command.Period.Start);
        Assert.Equal(end, command.Period.End);
    }

    [Fact]
    public void ToCommand_WithoutCompletePeriod_ThrowsInvalidArgument()
    {
        var request = new PaymentPeriodRequest
        {
            PayeeId = Guid.NewGuid().ToString(),
            PeriodStart = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var exception = Assert.Throws<RpcException>(() => request.ToCommand());

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public void ToCommand_WithNonIncreasingPeriod_ThrowsInvalidArgument()
    {
        var instant = Timestamp.FromDateTime(DateTime.UtcNow);
        var request = new PaymentPeriodRequest
        {
            PayeeId = Guid.NewGuid().ToString(),
            PeriodStart = instant,
            PeriodEnd = instant
        };

        var exception = Assert.Throws<RpcException>(() => request.ToCommand());

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public void ToCommand_WithInvalidTimestamp_ThrowsInvalidArgument()
    {
        var request = new PaymentPeriodRequest
        {
            PayeeId = Guid.NewGuid().ToString(),
            PeriodStart = new Timestamp { Seconds = long.MaxValue },
            PeriodEnd = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var exception = Assert.Throws<RpcException>(() => request.ToCommand());

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void RecentSettlementsToCommand_WithInvalidTake_ThrowsInvalidArgument(int take)
    {
        var request = new RecentSettlementsRequest
        {
            OwnerId = Guid.NewGuid().ToString(),
            Take = take
        };

        var exception = Assert.Throws<RpcException>(() => request.ToCommand());

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }
}
