using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentSessionFingerprintTests
{
    [Fact]
    public void Create_KnownSpecification_ReturnsStableVersionedHash()
    {
        var specification = Authorization(5000);

        var fingerprint = PaymentSessionFingerprint.Create(specification);

        Assert.Equal(1, fingerprint.Version);
        Assert.Equal(
            "0713BD4621CAFA28101B75D381417C803995731C4729E42E9611C2807882F2C6",
            fingerprint.Value);
    }

    [Fact]
    public void Create_ChangedImmutableValue_ReturnsDifferentHash()
    {
        var first = PaymentSessionFingerprint.Create(Authorization(5000));
        var second = PaymentSessionFingerprint.Create(Authorization(5001));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_UnknownVersion_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() =>
            PaymentSessionFingerprint.Create(Authorization(5000), 2));
    }

    [Fact]
    public void Create_ChangedSession_ReturnsDifferentHash()
    {
        var first = PaymentSessionFingerprint.Create(
            Authorization(5000, PaymentSession.OnSession, "pm_test"));
        var second = PaymentSessionFingerprint.Create(
            Authorization(5000, PaymentSession.OffSession, "pm_test"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Create_ChangedPaymentMethod_ReturnsDifferentHash()
    {
        var first = PaymentSessionFingerprint.Create(
            Authorization(5000, PaymentSession.OnSession, "pm_first"));
        var second = PaymentSessionFingerprint.Create(
            Authorization(5000, PaymentSession.OnSession, "pm_second"));

        Assert.NotEqual(first, second);
    }

    private static PaymentSessionSpecification Authorization(
        long amountMinor,
        PaymentSession session = PaymentSession.OnSession,
        string? paymentMethodId = null) =>
        PaymentSessionSpecification.Create(
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            PaymentSessionKind.Authorization,
            session,
            "escrow",
            "booking:42",
            "payer:7",
            "payee:9",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            paymentMethodId,
            "cus_test",
            "acct_test");
}
