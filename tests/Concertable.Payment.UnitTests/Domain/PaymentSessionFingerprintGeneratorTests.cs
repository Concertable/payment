using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentSessionFingerprintGeneratorTests
{
    [Fact]
    public void Create_KnownSpecification_ReturnsStableVersionedHash()
    {
        var specification = Authorization(5000);

        var fingerprint = PaymentSessionFingerprintGenerator.Create(specification);

        Assert.Equal(1, fingerprint.Version);
        Assert.Equal(
            "7D7DA62C3F1B8E327653F8F1D9BC0A40233AF6FFB2FC45926666DCEB2F2E5F2D",
            fingerprint.Value);
    }

    [Fact]
    public void Create_ChangedImmutableValue_ReturnsDifferentHash()
    {
        var first = PaymentSessionFingerprintGenerator.Create(Authorization(5000));
        var second = PaymentSessionFingerprintGenerator.Create(Authorization(5001));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_UnknownVersion_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            PaymentSessionFingerprintGenerator.Create(Authorization(5000), 2));
    }

    private static PaymentSessionSpecification Authorization(long amountMinor) =>
        PaymentSessionSpecification.Create(
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            PaymentSessionKind.Authorization,
            "escrow",
            "booking:42",
            "payer:7",
            "payee:9",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            "cus_test",
            "acct_test");
}
