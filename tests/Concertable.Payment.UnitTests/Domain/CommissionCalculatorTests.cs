using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class CommissionCalculatorTests
{
    private readonly CommissionCalculator sut = new();

    [Theory]
    [InlineData(10_000, 500, 500, 10_500)]
    [InlineData(101, 500, 5, 106)]
    [InlineData(10, 500, 1, 11)]
    public void Calculate_AppliesPercentageWithHalfUpRounding(
        long grossMinor,
        int rateBasisPoints,
        long expectedCommissionMinor,
        long expectedPayerTotalMinor)
    {
        var result = sut.Calculate(grossMinor, Currency.Gbp, rateBasisPoints, 0);

        Assert.Equal(expectedCommissionMinor, result.CommissionGrossMinor);
        Assert.Equal(expectedCommissionMinor, result.CommissionNetMinor);
        Assert.Equal(0, result.CommissionVatMinor);
        Assert.Equal(expectedPayerTotalMinor, result.PayerTotalMinor);
    }

    [Fact]
    public void Calculate_DecomposesVatInclusiveCommission()
    {
        var result = sut.Calculate(24_000, Currency.Gbp, 500, 2_000);

        Assert.Equal(1_200, result.CommissionGrossMinor);
        Assert.Equal(1_000, result.CommissionNetMinor);
        Assert.Equal(200, result.CommissionVatMinor);
        Assert.Equal(25_200, result.PayerTotalMinor);
    }

    [Fact]
    public void Calculate_RejectsUnsupportedCurrency()
    {
        var unsupported = (Currency)978;

        Assert.Throws<DomainException>(() => sut.Calculate(10_000, unsupported, 500, 0));
    }

    [Fact]
    public void Calculate_UsesCheckedArithmetic()
    {
        Assert.Throws<OverflowException>(() => sut.Calculate(long.MaxValue, Currency.Gbp, 10_000, 0));
    }

    [Fact]
    public void CalculateCumulativeRefund_MultiplePartialsReconcileExactlyAtFullRefund()
    {
        var firstCumulative = sut.CalculateCumulativeRefund(503, 3_333, 10_000);
        var secondCumulative = sut.CalculateCumulativeRefund(503, 6_666, 10_000);
        var finalCumulative = sut.CalculateCumulativeRefund(503, 10_000, 10_000);

        Assert.Equal(168, firstCumulative);
        Assert.Equal(335, secondCumulative);
        Assert.Equal(503, finalCumulative);
        Assert.Equal(503, firstCumulative + (secondCumulative - firstCumulative) + (finalCumulative - secondCumulative));
    }
}
