namespace Concertable.Payment.TestKit;

public static class StripeTestAccounts
{
    public static IReadOnlyDictionary<Guid, string> ByOwnerId { get; } =
        new Dictionary<Guid, string>
        {
            [new("2a39129f-1018-71cc-0127-8aa4bb21b80a")] = "acct_1TJiMePysoXmht10",
            [new("aa871783-c534-46d0-1be6-a008de110eaf")] = "acct_1TJiMoPupFslP2qz",
            [new("ccd6850f-4c9d-db0b-251d-825df8a66eef")] = "acct_1TJiMjLxk4aCq1Ui",
            [new("e77c2515-8340-7ee0-80ce-af0d76c2cfa9")] = "acct_1TJiPJLLwGSDilbV",
        };
}
