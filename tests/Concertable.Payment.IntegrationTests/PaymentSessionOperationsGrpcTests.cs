using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Contracts.Errors;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Extensions;
using Concertable.Payment.Infrastructure.Grpc;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Payment.Infrastructure.Services;
using Concertable.Testing.Integration;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Proto = Concertable.Payment.Grpc;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentSessionOperationsGrpcTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public PaymentSessionOperationsGrpcTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task CreateOrReplayAndGetStatus_OwningCaller_ReturnsSecretFreeSnapshot()
    {
        await using var context = await CreateContextAsync();
        var payerOwnerId = Guid.CreateVersion7();
        var payeeOwnerId = Guid.CreateVersion7();
        await SeedOwnersAsync(context, payerOwnerId, payeeOwnerId);
        var service = Service(context, new FakeStripeSessionClient(TimeProvider.System));
        var operationId = Guid.CreateVersion7();

        var descriptor = await service.CreateOrReplay(
            OperationRequest(operationId, payerOwnerId, payeeOwnerId),
            CallContext());
        var snapshot = await service.GetStatus(
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
        await using var context = await CreateContextAsync();
        var payerOwnerId = Guid.CreateVersion7();
        var payeeOwnerId = Guid.CreateVersion7();
        await SeedOwnersAsync(context, payerOwnerId, payeeOwnerId);
        var service = Service(context, new FakeStripeSessionClient(TimeProvider.System));
        var operationId = Guid.CreateVersion7();
        await service.CreateOrReplay(
            OperationRequest(operationId, payerOwnerId, payeeOwnerId),
            CallContext());

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.GetStatus(
            new Proto.PaymentSessionStatusRequest
            {
                OperationId = operationId.ToString("D"),
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
        await using var context = await CreateContextAsync();
        var payerOwnerId = Guid.CreateVersion7();
        var payeeOwnerId = Guid.CreateVersion7();
        await SeedOwnersAsync(context, payerOwnerId, payeeOwnerId);
        var provider = new FakeStripeSessionClient(TimeProvider.System);
        var service = Service(context, provider);
        var operationId = Guid.CreateVersion7();
        var created = await service.CreateOrReplay(
            OperationRequest(operationId, payerOwnerId, payeeOwnerId),
            CallContext());
        var attempt = await context.PaymentSessionAttempts.SingleAsync(
            value => value.AttemptId == Guid.Parse(created.Identity.AttemptId));
        attempt.ApplyTransition(new(
            PaymentOperationTransitionDisposition.Applied,
            Concertable.Payment.Contracts.PaymentOperationState.Failed,
            "failed",
            DateTimeOffset.UtcNow.AddSeconds(1),
            null,
            Concertable.Payment.Contracts.PaymentOperationTerminalDisposition.AttemptTerminal,
            Concertable.Payment.Contracts.PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
        await context.SaveChangesAsync();

        var retried = await service.Retry(
            new Proto.PaymentSessionRetryRequest
            {
                OperationId = operationId.ToString("D"),
                ExpectedAttemptId = created.Identity.AttemptId,
                ExpectedRevision = created.Identity.Revision,
                OwnerId = payerOwnerId.ToString("D")
            },
            CallContext());

        Assert.Equal(2, retried.Identity.Revision);
        Assert.NotEqual(created.Identity.AttemptId, retried.Identity.AttemptId);
        Assert.Equal(2, provider.ProviderObjectCount);
    }

    [Fact]
    public async Task CreateOrReplay_UnspecifiedKind_ReturnsInvalidArgument()
    {
        var service = new PaymentSessionOperationsGrpcService(new Mock<Application.Interfaces.IPaymentSessionService>().Object);

        var exception = await Assert.ThrowsAsync<RpcException>(() => service.CreateOrReplay(
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

        string[] methods = ["CreateOrReplay", "Retry", "GetStatus"];
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Where(endpoint => methods.Any(method => endpoint.DisplayName?.Contains(
                $"payment.PaymentSessionOperations/{method}",
                StringComparison.Ordinal) == true))
            .ToArray();

        Assert.Equal(3, endpoints.Length);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                authorization => authorization.Policy == "ServiceToken"));
    }

    private async Task<PaymentDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        var context = new PaymentDbContext(options, new PaymentConfigurationProvider());
        await context.Database.MigrateAsync();
        return context;
    }

    private static async Task SeedOwnersAsync(
        PaymentDbContext context,
        Guid payerOwnerId,
        Guid payeeOwnerId)
    {
        var payer = PayoutAccountEntity.Create(payerOwnerId, "payer@example.com");
        payer.LinkCustomer($"cus_{payerOwnerId:N}");
        var payee = PayoutAccountEntity.Create(payeeOwnerId, "payee@example.com");
        payee.LinkAccount($"acct_{payeeOwnerId:N}");
        context.PayoutAccounts.AddRange(payer, payee);
        await context.SaveChangesAsync();
    }

    private static PaymentSessionOperationsGrpcService Service(
        PaymentDbContext context,
        FakeStripeSessionClient provider) =>
        new(new PaymentSessionService(
            new PaymentSessionOperationRepository(context),
            new PaymentSessionAttemptRepository(context),
            new PayoutAccountRepository(context),
            provider,
            TimeProvider.System));

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
