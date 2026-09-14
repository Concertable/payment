using Concertable.Auth.Contracts;
using Concertable.Payment.Contracts;
using Xunit;

namespace Concertable.Payment.ArchitectureTests;

public sealed class PaymentScopeParityTests
{
    [Fact]
    public void PaymentWriteScope_MatchesTheScopeAuthIssues() =>
        Assert.Equal(AuthScope.PaymentWrite.Id, PaymentScopes.Write);

    [Fact]
    public void PaymentWriteScope_IsAcceptedByThePaymentResource() =>
        Assert.Contains(PaymentScopes.Write, AuthResource.Payment.AcceptedScopes.Select(scope => scope.Id));
}
