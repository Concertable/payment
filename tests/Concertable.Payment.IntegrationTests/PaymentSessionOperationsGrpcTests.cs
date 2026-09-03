using Concertable.Payment.Application.Interfaces;
using Concertable.Payment.Application.PaymentSessions;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Grpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentSessionOperationsGrpcTests
{
    private readonly Mock<IPaymentSessionService> paymentSessionService;
    private readonly PaymentSessionOperationsGrpcService sut;

    public PaymentSessionOperationsGrpcTests()
    {
        this.paymentSessionService = new Mock<IPaymentSessionService>();
        this.sut = new PaymentSessionOperationsGrpcService(this.paymentSessionService.Object);
    }

    [Fact]
    public async Task CreateOrReplayAndGetStatus_OwningCaller_ReturnsSecretFreeSnapshot()
    {
        var operationId = Guid.CreateVersion7();
        var attemptId = Guid.CreateVersion7();
        var payerOwnerId = Guid.CreateVersion7();
        var payeeOwnerId = Guid.CreateVersion7();
        var identity = new PaymentOperationIdentity(operationId, attemptId, 1);
        this.paymentSessionService
            .Setup(service => service.CreateOrReplayAsync(
                It.IsAny<PaymentSessionOperationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSessionExecution(
                identity,
                PaymentSessionKind.Authorization,
                PaymentOperationState.RequiresConfirmation,
                "client_secret",
                "customer_session_secret",
                $"cus_{payerOwnerId:N}"));
        this.paymentSessionService
            .Setup(service => service.RefreshAsync(
                It.IsAny<PaymentSessionStatusRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSessionStatus(
                identity,
                PaymentOperationState.RequiresConfirmation,
                PaymentOperationTerminalDisposition.NonTerminal,
                PaymentOperationRetryDisposition.ContinueCurrentAttempt,
                null,
                null,
                null));

        var descriptor = await this.sut.CreateOrReplay(
            OperationRequest(operationId, payerOwnerId, payeeOwnerId),
            CallContext());
        var snapshot = await this.sut.GetStatus(
            new Proto.PaymentSessionStatusRequest
            {
                OperationId = operationId.ToString("D"),
                OwnerId = payerOwnerId.ToString("D")
            },
            CallContext());

        Assert.Equal(operationId.ToString("D"), descriptor.Identity.OperationId);
        Assert.Equal(Proto.PaymentSessionKind.Authorization, descriptor.Kind);
        Assert.NotEmpty(descriptor.ClientSecret);
        Assert.NotEmpty(descriptor.CustomerSessionSecret);
        Assert.Equal($"cus_{payerOwnerId:N}", descriptor.CustomerToken);
        Assert.Equal(descriptor.Identity, snapshot.Identity);
        Assert.Equal(Proto.PaymentOperationState.RequiresConfirmation, snapshot.State);
        Assert.Equal(Proto.PaymentOperationTerminalDisposition.NonTerminal, snapshot.TerminalDisposition);
        Assert.Equal(Proto.PaymentOperationRetryDisposition.ContinueCurrentAttempt, snapshot.RetryDisposition);
        Assert.Null(snapshot.Failure);
    }

    [Fact]
    public async Task GetStatus_DifferentOwner_ReturnsTypedUnknownFailure()
    {
        this.paymentSessionService
            .Setup(service => service.RefreshAsync(
                It.IsAny<PaymentSessionStatusRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentOperationError.Unknown());

        var exception = await Assert.ThrowsAsync<RpcException>(() => this.sut.GetStatus(
            new Proto.PaymentSessionStatusRequest
            {
                OperationId = Guid.CreateVersion7().ToString("D"),
                OwnerId = Guid.CreateVersion7().ToString("D")
            },
            CallContext()));
        var detail = Proto.OperationErrorDetail.Parser.ParseFrom(
            Assert.Single(exception.Trailers, entry => entry.Key == PaymentGrpcErrors.TrailerKey).ValueBytes);

        Assert.Equal(StatusCode.Aborted, exception.StatusCode);
        Assert.Equal(new PaymentOperationError.Unknown().Definition.Code, detail.Code);
        Assert.Equal(new PaymentOperationError.Unknown().Definition.Message, detail.Message);
    }

    [Fact]
    public async Task Retry_EligibleCurrentAttempt_ReturnsNextRevision()
    {
        var operationId = Guid.CreateVersion7();
        var firstAttemptId = Guid.CreateVersion7();
        var nextAttemptId = Guid.CreateVersion7();
        var payerOwnerId = Guid.CreateVersion7();
        this.paymentSessionService
            .Setup(service => service.RetryAsync(
                It.IsAny<PaymentSessionRetryRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSessionExecution(
                new(operationId, nextAttemptId, 2),
                PaymentSessionKind.Authorization,
                PaymentOperationState.RequiresConfirmation,
                "client_secret",
                "customer_session_secret",
                "customer_token"));

        var retried = await this.sut.Retry(
            new Proto.PaymentSessionRetryRequest
            {
                OperationId = operationId.ToString("D"),
                ExpectedAttemptId = firstAttemptId.ToString("D"),
                ExpectedRevision = 1,
                OwnerId = payerOwnerId.ToString("D")
            },
            CallContext());

        Assert.Equal(2, retried.Identity.Revision);
        Assert.Equal(nextAttemptId.ToString("D"), retried.Identity.AttemptId);
    }

    [Fact]
    public async Task CreateOrReplay_UnspecifiedKind_ReturnsInvalidArgument()
    {
        var exception = await Assert.ThrowsAsync<RpcException>(() => this.sut.CreateOrReplay(
            new Proto.PaymentSessionOperationRequest
            {
                OperationId = Guid.CreateVersion7().ToString("D"),
                Kind = Proto.PaymentSessionKind.Unspecified,
                OperationType = "escrow",
                ConsumerCorrelation = "booking:42",
                PayerOwnerId = Guid.CreateVersion7().ToString("D"),
                FundsRouting = Proto.PaymentSessionFundsRouting.Destination
            },
            CallContext()));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task Routing_PaymentSessionOperationsRequiresServiceToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddGrpc();
        await using var app = builder.Build();
        app.MapPaymentGrpcServices();

        string[] methods = ["SetupPaymentMethod", "ValidatePaymentMethod", "CreateOrReplay", "Retry", "GetStatus"];
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => methods.Any(method => endpoint.DisplayName?.Contains(
                $"payment.PaymentSessionOperations/{method}",
                StringComparison.Ordinal) == true))
            .ToArray();

        Assert.Equal(5, endpoints.Length);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                authorization => authorization.Policy == "ServiceToken"));
    }

    private static Proto.PaymentSessionOperationRequest OperationRequest(
        Guid operationId,
        Guid payerOwnerId,
        Guid payeeOwnerId) =>
        new()
        {
            OperationId = operationId.ToString("D"),
            Kind = Proto.PaymentSessionKind.Authorization,
            OperationType = "escrow",
            ConsumerCorrelation = $"booking:{operationId:N}",
            PayerOwnerId = payerOwnerId.ToString("D"),
            PayeeOwnerId = payeeOwnerId.ToString("D"),
            AmountMinor = 5000,
            Currency = Proto.Currency.Gbp,
            FundsRouting = Proto.PaymentSessionFundsRouting.Destination
        };

    private static ServerCallContext CallContext() => new Mock<ServerCallContext>().Object;
}
