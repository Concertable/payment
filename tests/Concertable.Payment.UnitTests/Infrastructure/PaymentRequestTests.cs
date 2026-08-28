extern alias PaymentClient;

using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentRequestTests
{
    private static readonly IReadOnlyDictionary<string, string> Metadata =
        new Dictionary<string, string> { ["bookingId"] = "42", ["source"] = "test" };

    [Fact]
    public void ManagerPay_Create_WithOperationId_MapsRequest()
    {
        var operationId = Guid.CreateVersion7();
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();

        var request = Proto.ManagerPayRequest.Create(
            operationId,
            payerId,
            payeeId,
            Money.Gbp(50),
            "pm_test",
            PaymentSession.OffSession,
            42);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(payerId.ToString("D"), request.PayerId);
        Assert.Equal(payeeId.ToString("D"), request.PayeeId);
        Assert.Equal(5000, request.Amount.AmountMinor);
        Assert.Equal("pm_test", request.PaymentMethodId);
        Assert.Equal(Proto.PaymentSessionType.OffSession, request.Session);
        Assert.Equal(42, request.BookingId);
    }

    [Fact]
    public void ManagerPay_Create_EmptyOperationId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.ManagerPayRequest.Create(
            Guid.Empty,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            "pm_test",
            PaymentSession.OnSession,
            42));
    }

    [Fact]
    public void ReleaseByBookingId_Create_WithOperationId_MapsRequest()
    {
        var operationId = Guid.CreateVersion7();

        var request = Proto.ReleaseByBookingIdRequest.Create(operationId, 42);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(42, request.BookingId);
    }

    [Fact]
    public void ReleaseByBookingId_Create_EmptyOperationId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Proto.ReleaseByBookingIdRequest.Create(Guid.Empty, 42));
    }

    [Fact]
    public void Deposit_Create_MapsRequest()
    {
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();

        var request = Proto.DepositRequest.Create(
            payerId,
            payeeId,
            Money.Gbp(120),
            "pm_test",
            PaymentSession.OnSession,
            7);

        Assert.Equal(payerId.ToString("D"), request.PayerId);
        Assert.Equal(payeeId.ToString("D"), request.PayeeId);
        Assert.Equal(12000, request.Amount.AmountMinor);
        Assert.Equal("pm_test", request.PaymentMethodId);
        Assert.Equal(Proto.PaymentSessionType.OnSession, request.Session);
        Assert.Equal(7, request.BookingId);
    }

    [Fact]
    public void Deposit_Create_EmptyPayeeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.DepositRequest.Create(
            Guid.CreateVersion7(),
            Guid.Empty,
            Money.Gbp(120),
            "pm_test",
            PaymentSession.OnSession,
            7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deposit_Create_NonPositiveBookingId_Throws(int bookingId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.DepositRequest.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(120),
            "pm_test",
            PaymentSession.OnSession,
            bookingId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Deposit_Create_BlankPaymentMethodId_Throws(string paymentMethodId)
    {
        Assert.Throws<ArgumentException>(() => Proto.DepositRequest.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(120),
            paymentMethodId,
            PaymentSession.OnSession,
            7));
    }

    [Fact]
    public void Capture_Create_BlankPaymentIntentId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.CaptureRequest.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(60),
            " ",
            9));
    }

    [Fact]
    public void BoundCommissionManagerPay_Create_MapsRequest()
    {
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();
        var bindingId = Guid.CreateVersion7();

        var request = Proto.BoundCommissionManagerPayRequest.Create(
            payerId,
            payeeId,
            Money.Gbp(80),
            "pm_test",
            PaymentSession.OffSession,
            5,
            bindingId,
            "ext-ref",
            stripeSetupIntentId: null);

        Assert.Equal(bindingId.ToString("D"), request.CommissionBindingId);
        Assert.Equal("ext-ref", request.ExternalReference);
        Assert.Equal(string.Empty, request.StripeSetupIntentId);
    }

    [Fact]
    public void BoundCommissionManagerPay_Create_EmptyBindingId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.BoundCommissionManagerPayRequest.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(80),
            "pm_test",
            PaymentSession.OffSession,
            5,
            Guid.Empty,
            "ext-ref",
            stripeSetupIntentId: null));
    }

    [Fact]
    public void CustomerPay_Create_CopiesMetadata()
    {
        var request = Proto.CustomerPayRequest.Create(
            Guid.CreateVersion7(),
            concertId: 3,
            Guid.CreateVersion7(),
            Money.Gbp(25),
            "pm_test",
            Metadata);

        Assert.Equal(3, request.ConcertId);
        Assert.Equal("42", request.Metadata["bookingId"]);
        Assert.Equal("test", request.Metadata["source"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void CustomerPay_Create_NonPositiveConcertId_Throws(int concertId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Proto.CustomerPayRequest.Create(
            Guid.CreateVersion7(),
            concertId,
            Guid.CreateVersion7(),
            Money.Gbp(25),
            "pm_test",
            Metadata));
    }

    [Fact]
    public void CreatePaymentSession_Create_CopiesMetadata()
    {
        var request = Proto.CreatePaymentSessionRequest.Create(
            Guid.CreateVersion7(),
            concertId: 8,
            Guid.CreateVersion7(),
            Metadata);

        Assert.Equal(8, request.ConcertId);
        Assert.Equal("42", request.Metadata["bookingId"]);
    }

    [Fact]
    public void CreateSetupSession_Create_CopiesMetadata()
    {
        var payerId = Guid.CreateVersion7();

        var request = Proto.CreateSetupSessionRequest.Create(payerId, Metadata);

        Assert.Equal(payerId.ToString("D"), request.PayerId);
        Assert.Equal("test", request.Metadata["source"]);
    }

    [Fact]
    public void CreateSetupSession_Create_EmptyPayerId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Proto.CreateSetupSessionRequest.Create(Guid.Empty, Metadata));
    }

    [Fact]
    public void CreateHoldSession_Create_MapsAmountAndMetadata()
    {
        var request = Proto.CreateHoldSessionRequest.Create(
            Guid.CreateVersion7(),
            Money.Gbp(200),
            Metadata);

        Assert.Equal(20000, request.Amount.AmountMinor);
        Assert.Equal("42", request.Metadata["bookingId"]);
    }

    [Fact]
    public void CreateOrBindCommission_Create_NullOptionalStrings_BecomeEmpty()
    {
        var configId = Guid.CreateVersion7();

        var request = Proto.CreateOrBindCommissionRequest.Create(
            "ext-ref",
            "payer-ref",
            Currency.Gbp,
            configId,
            stripePaymentIntentId: null,
            stripeSetupIntentId: null);

        Assert.Equal("ext-ref", request.ExternalReference);
        Assert.Equal("payer-ref", request.PayerReference);
        Assert.Equal(configId.ToString("D"), request.ReviewedCommissionConfigurationId);
        Assert.Equal(string.Empty, request.StripePaymentIntentId);
        Assert.Equal(string.Empty, request.StripeSetupIntentId);
    }

    [Fact]
    public void CreateOrBindCommission_Create_BlankExternalReference_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.CreateOrBindCommissionRequest.Create(
            " ",
            "payer-ref",
            Currency.Gbp,
            Guid.CreateVersion7(),
            stripePaymentIntentId: null,
            stripeSetupIntentId: null));
    }

    [Fact]
    public void ConfirmReviewedGross_Create_EmptyBindingId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.ConfirmReviewedGrossRequest.Create(
            Guid.Empty,
            "ext-ref",
            "payer-ref",
            Money.Gbp(10)));
    }

    [Fact]
    public void PreviewCommission_Create_MapsGross()
    {
        var request = Proto.PreviewCommissionRequest.Create(Money.Gbp(15));

        Assert.Equal(1500, request.Gross.AmountMinor);
    }

    [Fact]
    public void FindHeldIntent_Create_MapsRequest()
    {
        var payerId = Guid.CreateVersion7();

        var request = Proto.FindHeldIntentRequest.Create(payerId, 11);

        Assert.Equal(payerId.ToString("D"), request.PayerId);
        Assert.Equal(11, request.ApplicationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void FindHeldIntent_Create_NonPositiveApplicationId_Throws(int applicationId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Proto.FindHeldIntentRequest.Create(Guid.CreateVersion7(), applicationId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RecentSettlements_Create_NonPositiveTake_Throws(int take)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Proto.RecentSettlementsRequest.Create(Guid.CreateVersion7(), take));
    }

    [Fact]
    public void PaymentPeriod_Create_MapsPayeeAndTimestamps()
    {
        var payeeId = Guid.CreateVersion7();
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var period = new DateRange(start, start.AddMonths(1));

        var request = Proto.PaymentPeriodRequest.Create(payeeId, period);

        Assert.Equal(payeeId.ToString("D"), request.PayeeId);
        Assert.Equal(start, request.PeriodStart.ToDateTime());
        Assert.Equal(start.AddMonths(1), request.PeriodEnd.ToDateTime());
    }

    [Fact]
    public void PayoutOwner_Create_EmptyOwnerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Proto.PayoutOwnerRequest.Create(Guid.Empty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefundByBookingId_Create_NonPositiveBookingId_Throws(int bookingId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Proto.RefundByBookingIdRequest.Create(bookingId));
    }

    [Fact]
    public void BoundCommissionRefundByBookingId_Create_MapsRequest()
    {
        var request = Proto.BoundCommissionRefundByBookingIdRequest.Create(6, Money.Gbp(30));

        Assert.Equal(6, request.BookingId);
        Assert.Equal(3000, request.Gross.AmountMinor);
    }
}
