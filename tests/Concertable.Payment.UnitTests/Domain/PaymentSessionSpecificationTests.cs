using Concertable.Kernel;
using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Domain;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class PaymentSessionSpecificationTests
{
    [Fact]
    public void Create_UuidV4OperationId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => PaymentSessionSpecification.Create(
            Guid.NewGuid(),
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OnSession,
            "setup",
            "profile:42",
            "payer:7",
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            "cus_test",
            null));
    }

    [Fact]
    public void Create_PaymentWithoutAmount_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => PaymentSessionSpecification.Create(
            Guid.CreateVersion7(),
            PaymentSessionKind.Payment,
            PaymentSession.OnSession,
            "ticket",
            "purchase:42",
            "payer:7",
            "payee:9",
            null,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            null,
            "cus_test",
            "acct_test"));
    }

    [Fact]
    public void Create_SetupWithMoneyMovement_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => PaymentSessionSpecification.Create(
            Guid.CreateVersion7(),
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OnSession,
            "setup",
            "profile:42",
            "payer:7",
            "payee:9",
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            null,
            "cus_test",
            "acct_test"));
    }

    [Fact]
    public void Create_ValidSetup_NormalizesImmutableStrings()
    {
        var specification = PaymentSessionSpecification.Create(
            Guid.CreateVersion7(),
            PaymentSessionKind.PaymentMethodSetup,
            PaymentSession.OffSession,
            " setup ",
            " profile:42 ",
            " payer:7 ",
            null,
            null,
            null,
            PaymentSessionFundsRouting.None,
            null,
            " cus_test ",
            null);

        Assert.Equal("setup", specification.OperationType);
        Assert.Equal("profile:42", specification.ConsumerCorrelation);
        Assert.Equal("payer:7", specification.PayerOwnerKey);
        Assert.Equal("cus_test", specification.ProviderCustomerId);
        Assert.Equal(PaymentSessionCaptureMode.None, specification.CaptureMode);
        Assert.Equal(PaymentSession.OffSession, specification.Session);
    }

    [Fact]
    public void Create_OffSessionPaymentWithoutPaymentMethod_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => PaymentSessionSpecification.Create(
            Guid.CreateVersion7(),
            PaymentSessionKind.Payment,
            PaymentSession.OffSession,
            "ticket",
            "purchase:42",
            "payer:7",
            "payee:9",
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            null,
            "cus_test",
            "acct_test"));
    }

    [Fact]
    public void Create_OffSessionPayment_NormalizesPaymentMethod()
    {
        var specification = PaymentSessionSpecification.Create(
            Guid.CreateVersion7(),
            PaymentSessionKind.Payment,
            PaymentSession.OffSession,
            "ticket",
            "purchase:42",
            "payer:7",
            "payee:9",
            5000,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            " pm_test ",
            "cus_test",
            "acct_test");

        Assert.Equal(PaymentSession.OffSession, specification.Session);
        Assert.Equal("pm_test", specification.PaymentMethodId);
    }
}
