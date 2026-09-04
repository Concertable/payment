using Concertable.Kernel.ValueObjects;
using Concertable.Payment.Contracts;
using Concertable.Payment.Domain;
using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;
using Concertable.Payment.Domain.ProviderContract;
using Concertable.Payment.Infrastructure.Data;
using Concertable.Payment.Infrastructure.Repositories;
using Concertable.Testing.Integration;
using Microsoft.EntityFrameworkCore;

namespace Concertable.Payment.IntegrationTests;

public sealed class PaymentSessionPersistenceTests : IClassFixture<SqlFixture>
{
    private readonly SqlFixture sql;

    public PaymentSessionPersistenceTests(SqlFixture sql)
    {
        this.sql = sql;
    }

    [Fact]
    public async Task ReserveInitialAsync_ConcurrentSameSpecification_ConvergesOnOneAttempt()
    {
        await MigrateAsync();
        var specification = Specification(Guid.CreateVersion7());
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;

        async Task<PaymentSessionReservation> ReserveAsync()
        {
            await using var context = CreateContext();
            var repository = new PaymentSessionOperationRepository(context);
            if (Interlocked.Increment(ref readyCount) == 2)
                ready.SetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return await repository.ReserveInitialAsync(specification, DateTimeOffset.UtcNow);
        }

        var reservations = await Task.WhenAll(ReserveAsync(), ReserveAsync());

        Assert.Contains(reservations, value => value.Disposition == PaymentSessionReservationDisposition.Created);
        Assert.Contains(reservations, value => value.Disposition == PaymentSessionReservationDisposition.Replayed);
        Assert.All(reservations, value => Assert.Equal(
            reservations[0].Attempt!.AttemptId,
            value.Attempt!.AttemptId));
        await using var verification = CreateContext();
        Assert.Equal(
            1,
            await verification.PaymentSessionOperations.CountAsync(
                operation => operation.OperationId == specification.OperationId));
        Assert.Equal(
            1,
            await verification.PaymentSessionAttempts.CountAsync(
                attempt => attempt.OperationId == specification.OperationId));
        var operation = await verification.PaymentSessionOperations.SingleAsync(
            value => value.OperationId == specification.OperationId);
        Assert.Equal(PaymentSession.OffSession, operation.Session);
        Assert.Equal(specification.PaymentMethodId, operation.PaymentMethodId);
    }

    [Fact]
    public async Task ReserveInitialAsync_ChangedSpecification_ReturnsConflictWithoutMutation()
    {
        await MigrateAsync();
        var operationId = Guid.CreateVersion7();
        await using var firstContext = CreateContext();
        var firstRepository = new PaymentSessionOperationRepository(firstContext);
        await firstRepository.ReserveInitialAsync(Specification(operationId), DateTimeOffset.UtcNow);

        await using var secondContext = CreateContext();
        var reservation = await new PaymentSessionOperationRepository(secondContext)
            .ReserveInitialAsync(Specification(operationId, 5001), DateTimeOffset.UtcNow);

        Assert.Equal(PaymentSessionReservationDisposition.Conflict, reservation.Disposition);
        Assert.Null(reservation.Attempt);
        Assert.Equal(
            1,
            await secondContext.PaymentSessionAttempts.CountAsync(
                attempt => attempt.OperationId == operationId));
    }

    [Fact]
    public async Task ProviderBinding_DuplicateProviderObject_IsRejectedByUniqueIndex()
    {
        await MigrateAsync();
        var providerObjectId = $"pi_{Guid.NewGuid():N}";
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await new PaymentSessionOperationRepository(firstContext)
            .ReserveInitialAsync(Specification(Guid.CreateVersion7()), DateTimeOffset.UtcNow);
        var second = await new PaymentSessionOperationRepository(secondContext)
            .ReserveInitialAsync(Specification(Guid.CreateVersion7()), DateTimeOffset.UtcNow);
        first.Attempt!.BindProviderObject(providerObjectId);
        second.Attempt!.BindProviderObject(providerObjectId);

        await firstContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ReserveNextAttemptAsync_DuplicatePredecessor_ReplaysPersistedSuccessor()
    {
        await MigrateAsync();
        var operationId = Guid.CreateVersion7();
        var predecessor = await SeedFailedOperationAsync(operationId);
        await using var firstContext = CreateContext();
        var first = await new PaymentSessionOperationRepository(firstContext).ReserveNextAttemptAsync(
            operationId,
            predecessor.AttemptId,
            predecessor.Revision,
            DateTimeOffset.UtcNow);

        await using var secondContext = CreateContext();
        var replay = await new PaymentSessionOperationRepository(secondContext).ReserveNextAttemptAsync(
            operationId,
            predecessor.AttemptId,
            predecessor.Revision,
            DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(PaymentSessionReservationDisposition.Created, first.Disposition);
        Assert.Equal(PaymentSessionReservationDisposition.Replayed, replay.Disposition);
        Assert.Equal(first.Attempt!.AttemptId, replay.Attempt!.AttemptId);
        Assert.Equal(2, replay.Attempt.Revision);
        Assert.Equal(
            2,
            await secondContext.PaymentSessionAttempts.CountAsync(
                attempt => attempt.OperationId == operationId));
    }

    [Fact]
    public async Task OperationRowVersion_ConcurrentUpdates_RejectsSecondWriter()
    {
        await MigrateAsync();
        var operationId = Guid.CreateVersion7();
        await using (var seedContext = CreateContext())
        {
            await new PaymentSessionOperationRepository(seedContext)
                .ReserveInitialAsync(Specification(operationId), DateTimeOffset.UtcNow);
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await firstContext.PaymentSessionOperations
            .SingleAsync(operation => operation.OperationId == operationId);
        var second = await secondContext.PaymentSessionOperations
            .SingleAsync(operation => operation.OperationId == operationId);
        firstContext.Entry(first).Property(operation => operation.CanceledAt).CurrentValue =
            DateTimeOffset.UtcNow;
        secondContext.Entry(second).Property(operation => operation.CanceledAt).CurrentValue =
            DateTimeOffset.UtcNow.AddSeconds(1);

        await firstContext.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private async Task<PaymentSessionAttemptEntity> SeedFailedOperationAsync(Guid operationId)
    {
        await using var context = CreateContext();
        var reservation = await new PaymentSessionOperationRepository(context)
            .ReserveInitialAsync(Specification(operationId), DateTimeOffset.UtcNow);
        var attempt = reservation.Attempt!;
        attempt.BindProviderObject($"pi_{Guid.NewGuid():N}");
        attempt.ApplyTransition(PaymentSessionKind.Authorization, new(
            PaymentOperationState.Failed,
            "failed",
            DateTimeOffset.UtcNow,
            null,
            PaymentOperationTerminalDisposition.AttemptTerminal,
            PaymentOperationRetryDisposition.CreateNewAttempt,
            PaymentOperationFailure.FromCode(PaymentOperationFailureCode.Declined)));
        await context.SaveChangesAsync();
        return attempt;
    }

    private async Task MigrateAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private PaymentDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        return new PaymentDbContext(options, new PaymentConfigurationProvider());
    }

    private static PaymentSessionDefinition Specification(Guid operationId, long amountMinor = 5000) =>
        PaymentSessionDefinition.Create(
            operationId,
            PaymentSessionKind.Authorization,
            PaymentSession.OffSession,
            "escrow",
            $"order:{operationId:N}",
            $"payer:{operationId:N}",
            $"payee:{operationId:N}",
            amountMinor,
            Currency.Gbp,
            PaymentSessionFundsRouting.Destination,
            $"pm_{operationId:N}",
            $"cus_{operationId:N}",
            $"acct_{operationId:N}",
            null);
}
