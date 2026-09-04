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

        Assert.Equal(2, fingerprint.Version);
        Assert.Equal(
            "83DDA7499170450C37BEC169220DC7062EE179A5C43330DE44EBA4270C4336D6",
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
            PaymentSessionFingerprint.Create(Authorization(5000), 3));
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

    [Fact]
    public void Create_ChangedMandateTerms_ReturnsDifferentHash()
    {
        var first = PaymentSessionFingerprint.Create(Setup("venue-hire-mandate-v1"));
        var second = PaymentSessionFingerprint.Create(Setup("venue-hire-mandate-v2"));

        Assert.NotEqual(first, second);
    }

    private static PaymentSessionDefinition Setup(string mandateTermsVersion) =>
        PaymentSessionDefinition.Create(
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OnSession,
            "purchase",
            "order:42",
            "payer:7",
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            "cus_test",
            null,
            mandateTermsVersion);

    private static PaymentSessionDefinition Authorization(
        long amountMinor,
        PaymentSession session = PaymentSession.OnSession,
        string? paymentMethodId = null) =>
        PaymentSessionDefinition.Create(
            Guid.Parse("018f3d73-b5db-7a21-96f2-62a5f0a1d4c2"),
            PaymentSessionKind.Authorization,
            session,
            "escrow",
            "order:42",
            "payer:7",
            "payee:9",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            paymentMethodId,
            "cus_test",
            "acct_test",
            null);
}
