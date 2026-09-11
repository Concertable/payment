extern alias PaymentClient;

using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Proto = PaymentClient::Concertable.Payment.Grpc;

namespace Concertable.Payment.UnitTests.Infrastructure;

public sealed class PaymentRequestTests
{
    private static readonly PaymentOperationReference Reference = new("settlement", "order:42");
    private static readonly PaymentOperationReference PaymentMethod = new("paymentMethod", "wallet:42");

    [Fact]
    public void SettlementPayment_Create_MapsOpaqueReferences()
    {
        var operationId = Guid.CreateVersion7();
        var payerId = Guid.CreateVersion7();
        var payeeId = Guid.CreateVersion7();

        var request = Proto.SettlementPaymentRequest.Create(
            operationId,
            Reference,
            payerId,
            payeeId,
            Money.Gbp(50),
            PaymentMethod,
            PaymentSession.OffSession);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(Reference.OperationType, request.Reference.OperationType);
        Assert.Equal(Reference.ClientReference, request.Reference.ClientReference);
        Assert.Equal(PaymentMethod.OperationType, request.PaymentMethod.OperationType);
        Assert.Equal(PaymentMethod.ClientReference, request.PaymentMethod.ClientReference);
        Assert.Equal(5000, request.Amount.AmountMinor);
    }

    [Fact]
    public void Deposit_Create_MapsOpaqueReferences()
    {
        var request = Proto.DepositRequest.Create(
            Guid.CreateVersion7(),
            Reference,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            PaymentMethod,
            PaymentSession.OnSession);

        Assert.Equal(Reference.OperationType, request.Reference.OperationType);
        Assert.Equal(PaymentMethod.ClientReference, request.PaymentMethod.ClientReference);
    }

    [Fact]
    public void Capture_Create_MapsAuthorizationReference()
    {
        var authorization = new PaymentOperationReference("authorization", "authorization:42");

        var request = Proto.CaptureRequest.Create(
            Guid.CreateVersion7(),
            Reference,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            authorization);

        Assert.Equal(authorization.OperationType, request.Authorization.OperationType);
        Assert.Equal(authorization.ClientReference, request.Authorization.ClientReference);
    }

    [Fact]
    public void ReleaseEscrow_Create_MapsReference()
    {
        var operationId = Guid.CreateVersion7();

        var request = Proto.ReleaseEscrowRequest.Create(operationId, Reference);

        Assert.Equal(operationId.ToString("D"), request.OperationId);
        Assert.Equal(Reference.ClientReference, request.Reference.ClientReference);
    }

    [Fact]
    public void BoundCommissionRefund_Create_MapsReason()
    {
        var request = Proto.BoundCommissionRefundRequest.Create(
            Reference,
            Money.Gbp(50),
            RefundReasonCodes.RequestedByPayer);

        Assert.Equal(RefundReasonCodes.RequestedByPayer, request.Reason);
        Assert.True(request.HasReason);
    }

    [Fact]
    public void SettlementPayment_Create_EmptyOperationId_Throws() =>
        Assert.Throws<ArgumentException>(() => Proto.SettlementPaymentRequest.Create(
            Guid.Empty,
            Reference,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            PaymentMethod,
            PaymentSession.OnSession));

    [Theory]
    [InlineData("", "order:42")]
    [InlineData("settlement", "")]
    public void Deposit_Create_InvalidReference_Throws(string operationType, string clientReference) =>
        Assert.Throws<ArgumentException>(() => Proto.DepositRequest.Create(
            Guid.CreateVersion7(),
            new(operationType, clientReference),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Money.Gbp(50),
            PaymentMethod,
            PaymentSession.OnSession));

    [Fact]
    public void CreateOrBindCommission_DoesNotPublishProviderIdentifiers()
    {
        var request = Proto.CreateOrBindCommissionRequest.Create(
            "order:42",
            "payer:42",
            Currency.Gbp,
            Guid.CreateVersion7());

        Assert.Equal("order:42", request.ExternalReference);
        Assert.Equal("payer:42", request.PayerReference);
    }
}
