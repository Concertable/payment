using Concertable.Payment.Application.PaymentSessions;

namespace Concertable.Payment.UnitTests;

public sealed class PaymentSessionIdempotencyKeyTests
{
    [Fact]
    public void Equals_SameComponents_ReturnsTrue()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();

        var first = new PaymentSessionIdempotencyKey(operationId, attemptId, 1);
        var second = new PaymentSessionIdempotencyKey(operationId, attemptId, 1);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ToString_ValidComponents_ReturnsCanonicalKey()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        var key = new PaymentSessionIdempotencyKey(operationId, attemptId, 1);

        var value = key.ToString();

        Assert.Equal($"payment-session:{operationId:D}:{attemptId:D}:1:create", value);
    }
}
