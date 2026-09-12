using Concertable.Auth.Contracts;
using Concertable.Payment.Contracts;
using Xunit;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentScopeParityTests
{
    [Fact]
    public void PaymentWriteScope_MatchesTheScopeAuthIssues() =>
        Assert.Equal(AuthScope.PaymentWrite.Id(), PaymentScopes.Write);

    [Fact]
    public void PaymentAudience_MatchesTheResourceAuthRegisters() =>
        Assert.Contains(AuthScope.PaymentWrite, AuthResource.Payment.AcceptedScopes());
}
