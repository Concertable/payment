using Concertable.Payment.Infrastructure.Mappers;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentSessionMappersTests
{
    [Theory]
    [InlineData(PaymentSession.OnSession, "on_session")]
    [InlineData(PaymentSession.OffSession, "off_session")]
    public void ToStripeUsage_DefinedSession_ReturnsProviderValue(
        PaymentSession session,
        string expected)
    {
        var value = session.ToStripeUsage();

        Assert.Equal(expected, value);
    }

    [Fact]
    public void ToStripeUsage_UndefinedSession_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((PaymentSession)100).ToStripeUsage());
    }
}
