using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class FinancialOperationEntityTests
{
    private static readonly PaymentOperationReference Reference = new("escrow", "order:17");

    [Fact]
    public void EnsureMatches_ReusedIdWithDifferentRequest_ThrowsInvalidOperationException()
    {
        var operation = FinancialOperationEntity.Create(
            Guid.NewGuid(),
            Reference,
            "fingerprint-a",
            DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            operation.EnsureMatches(Reference, "fingerprint-b"));

        Assert.Contains(operation.Id.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Succeed_PendingOperation_RecordsTerminalReference()
    {
        var completedAt = DateTimeOffset.UtcNow;
        var operation = FinancialOperationEntity.Create(
            Guid.NewGuid(),
            Reference,
            "fingerprint",
            completedAt.AddMinutes(-1));

        operation.Succeed("pi_test", completedAt);

        Assert.Equal(FinancialOperationStatus.Succeeded, operation.Status);
        Assert.Equal("pi_test", operation.ReferenceId);
        Assert.Equal(completedAt, operation.CompletedAt);
    }

    [Fact]
    public void Reject_TerminalOperation_ThrowsInvalidOperationException()
    {
        var operation = FinancialOperationEntity.Create(
            Guid.NewGuid(),
            Reference,
            "fingerprint",
            DateTimeOffset.UtcNow);
        operation.Succeed("re_test", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            operation.Reject("payment.rejected", "Rejected", DateTimeOffset.UtcNow));
    }
}
