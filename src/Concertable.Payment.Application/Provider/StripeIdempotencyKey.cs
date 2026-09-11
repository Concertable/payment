using System.Collections.Frozen;
using System.Globalization;

namespace Concertable.Payment.Application.Provider;

internal readonly record struct StripeIdempotencyKey
{
    private const long InitialRevision = 1;

    private static readonly FrozenDictionary<StripeIdempotencyScope, string> scopeNames =
        new Dictionary<StripeIdempotencyScope, string>
        {
            [StripeIdempotencyScope.PaymentSession] = "payment-session",
            [StripeIdempotencyScope.FinancialOperation] = "financial-operation",
            [StripeIdempotencyScope.CommissionBinding] = "commission-binding"
        }.ToFrozenDictionary();

    private readonly StripeIdempotencyScope scope;
    private readonly Guid identityId;
    private readonly Guid attemptId;
    private readonly long revision;
    private readonly string action;

    private StripeIdempotencyKey(
        StripeIdempotencyScope scope,
        Guid identityId,
        Guid attemptId,
        long revision,
        string action)
    {
        this.scope = scope;
        this.identityId = identityId;
        this.attemptId = attemptId;
        this.revision = revision;
        this.action = action;
    }

    public static StripeIdempotencyKey ForSessionAttempt(Guid operationId, Guid attemptId, long revision) =>
        new(StripeIdempotencyScope.PaymentSession, operationId, attemptId, revision, "create");

    public static StripeIdempotencyKey ForAttempt(
        StripeIdempotencyScope scope,
        Guid identityId,
        Guid attemptId,
        string action) =>
        new(scope, identityId, attemptId, InitialRevision, action);

    public static StripeIdempotencyKey ForSingleAttempt(
        StripeIdempotencyScope scope,
        Guid identityId,
        string action) =>
        new(scope, identityId, identityId, InitialRevision, action);

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{scopeNames[scope]}:{identityId:D}:{attemptId:D}:{revision}:{action}");
}
