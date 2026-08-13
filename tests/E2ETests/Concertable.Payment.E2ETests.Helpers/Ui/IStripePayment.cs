namespace Concertable.Payment.E2ETests.Helpers.Ui;

public interface IStripePayment
{
    Task PayWithSavedCardAsync();
    Task PayWithNewCardAsync(string cardNumber);
    Task CompleteChallengeAsync();
    Task CompleteChallengeIfRequiredAsync();
    Task FailChallengeAsync();
}
