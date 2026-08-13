using Concertable.Payment.Domain.Entities;
using Concertable.Payment.Domain.Enums;

namespace Concertable.Payment.UnitTests.Domain;

public sealed class FinancialOperationEntityTests
{
    [Fact]
    public void EnsureMatches_ReusedIdWithDifferentRequest_ThrowsInvalidOperationException()
    {
        var operation = FinancialOperationEntity.Create(
            Guid.NewGuid(),
            17,
            "fingerprint-a",
            DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            operation.EnsureMatches(17, "fingerprint-b"));

        Assert.Contains(operation.Id.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Succeed_PendingOperation_RecordsTerminalReference()
    {
        var completedAt = DateTimeOffset.UtcNow;
        var operation = FinancialOperationEntity.Create(
            Guid.NewGuid(),
            17,
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
            17,
            "fingerprint",
            DateTimeOffset.UtcNow);
        operation.Succeed("re_test", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            operation.Reject("payment.rejected", "Rejected", DateTimeOffset.UtcNow));
    }
}
