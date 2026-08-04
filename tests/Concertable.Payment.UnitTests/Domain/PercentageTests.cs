using Concertable.Kernel;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PercentageTests
{
    [Theory]
    [InlineData(10_000, 5, 500)]
    [InlineData(101, 5, 5)]
    [InlineData(10, 5, 1)]
    public void ApplyTo_RoundsHalfUp(
        long amount,
        decimal percentage,
        long expected)
    {
        Assert.Equal(expected, Percentage.From(percentage).ApplyTo(amount));
    }

    [Fact]
    public void ExcludeFrom_RemovesPercentageFromInclusiveAmount()
    {
        Assert.Equal(1_000, Percentage.From(20m).ExcludeFrom(1_200));
    }

    [Fact]
    public void From_RejectsUnsupportedPrecision()
    {
        Assert.Throws<DomainException>(() => Percentage.From(5.00001m));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void From_RejectsOutOfRangeValue(decimal value)
    {
        Assert.Throws<DomainException>(() => Percentage.From(value));
    }
}
