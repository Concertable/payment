namespace Concertable.E2ETests.Support;

public interface IStripePayment
{
    Task PayWithSavedCardAsync();
    Task PayWithNewCardAsync(string cardNumber);
    Task CompleteChallengeAsync();
    Task CompleteChallengeIfRequiredAsync();
    Task FailChallengeAsync();
}
