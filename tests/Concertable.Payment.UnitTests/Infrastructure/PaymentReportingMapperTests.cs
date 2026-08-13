extern alias PaymentClient;

using Google.Protobuf.WellKnownTypes;
using PaymentMappers = PaymentClient::Concertable.Payment.Client.Adapters.PaymentMappers;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentReportingMapperTests
{
    [Fact]
    public void ToMonthlyPaymentPoint_MapsDateMoneyAndCount()
    {
        var point = new Proto.MonthlyPaymentPointResponse
        {
            Month = Timestamp.FromDateTime(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)),
            Gross = new Proto.Money { AmountMinor = 1200, Currency = Proto.Currency.Gbp },
            Net = new Proto.Money { AmountMinor = 1000, Currency = Proto.Currency.Gbp },
            Count = 3
        };

        var result = PaymentMappers.ToMonthlyPaymentPoint(point);

        Assert.Equal(new DateOnly(2026, 8, 1), result.Month);
        Assert.Equal(1200, result.Gross.ToMinorUnits());
        Assert.Equal(1000, result.Net.ToMinorUnits());
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ToManagerSettlement_MapsIdentifiersMoneyAndTimestamp()
    {
        var payerId = Guid.NewGuid();
        var payeeId = Guid.NewGuid();
        var at = new DateTime(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
        var settlement = new Proto.SettlementReportItemResponse
        {
            Id = 4,
            BookingId = 7,
            PayerId = payerId.ToString(),
            PayeeId = payeeId.ToString(),
            Amount = new Proto.Money { AmountMinor = 2500, Currency = Proto.Currency.Gbp },
            At = Timestamp.FromDateTime(at)
        };

        var result = PaymentMappers.ToManagerSettlement(settlement);

        Assert.Equal(4, result.Id);
        Assert.Equal(7, result.BookingId);
        Assert.Equal(payerId, result.PayerId);
        Assert.Equal(payeeId, result.PayeeId);
        Assert.Equal(2500, result.Amount.ToMinorUnits());
        Assert.Equal(at, result.At);
    }
}
