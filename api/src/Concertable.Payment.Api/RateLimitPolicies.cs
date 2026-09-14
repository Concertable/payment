namespace Concertable.Payment.Api;

public static class RateLimitPolicies
{
    public const string SetupIntent = "setup-intent";

    public static readonly IReadOnlyList<string> All = [SetupIntent];
}
