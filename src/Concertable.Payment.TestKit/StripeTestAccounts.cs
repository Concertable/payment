namespace Concertable.Payment.TestKit;

public static class StripeTestAccounts
{
    public static IReadOnlyDictionary<Guid, string> BySeedUserId { get; } =
        new Dictionary<Guid, string>
        {
            [new("a1000000-0000-0000-0000-000000000001")] = "acct_1TJiMePysoXmht10",
            [new("a1000000-0000-0000-0000-000000000002")] = "acct_1TJiMoPupFslP2qz",
            [new("b1000000-0000-0000-0000-000000000001")] = "acct_1TJiMjLxk4aCq1Ui",
            [new("b1000000-0000-0000-0000-000000000002")] = "acct_1TJiPJLLwGSDilbV",
        };
}
