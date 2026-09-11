using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class SettlementOperationFingerprintTests
{
    [Fact]
    public void CreateCharge_SameRequest_ReturnsSameVersionedHash()
    {
        var operationId = Guid.CreateVersion7();

        var first = CreateCharge(operationId, Money.Gbp(50));
        var second = CreateCharge(operationId, Money.Gbp(50));

        Assert.Equal(SettlementOperationFingerprint.CurrentVersion, first.Version);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Value.Length);
    }

    [Fact]
    public void CreateCharge_ChangedAmount_ReturnsDifferentHash()
    {
        var operationId = Guid.CreateVersion7();

        Assert.NotEqual(
            CreateCharge(operationId, Money.Gbp(50)),
            CreateCharge(operationId, Money.Gbp(51)));
    }

    [Fact]
    public void CreateCharge_NonVersionSevenOperationId_Throws()
    {
        Assert.Throws<DomainException>(() => CreateCharge(Guid.NewGuid(), Money.Gbp(50)));
    }

    private static SettlementOperationFingerprint CreateCharge(Guid operationId, Money amount) =>
        SettlementOperationFingerprint.CreateCharge(
            operationId,
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c3"),
            amount,
            Money.Gbp(12),
            "pm_test",
            PaymentSession.OffSession,
            new("settlement", "order:42"));
}
