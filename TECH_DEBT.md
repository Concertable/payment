# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## MEDIUM

### Operation-less settlement overloads remain until consumers carry durable operation identities

Every outgoing Payment protobuf request is now created through one validated boundary: each generated
request partial class carries a `Create` factory (required identifiers non-empty, positive numeric keys,
non-empty strings), and every client adapter routes through it; the session-operation requests validate in
their `ToProto()` mappers. Proto3 defaults can no longer turn an omitted value into a valid-looking wire
value. What remains is the operation-less manager-pay and escrow-release **client overloads**, kept
temporarily for consumers that have not adopted durable operation identities.

**Resolves when:** the operation-less settlement overloads are removed after all consumers provide durable
operation identities.

## LOW

### Result extraction relies on null-forgiving assertions

Payment's RPC and application adapters use `TryGetError` after proving a result is not successful,
but Reunion exposes the extracted error as nullable. Call sites therefore use `error!` to recover an
invariant that the result type knows at runtime but its extraction API does not express to C#'s nullable
analysis.

**Resolves when:** Reunion provides an exhaustive match or failure accessor whose return type is non-null
on the failure branch, and Payment migrates its result projections without null-forgiving assertions.

### Internal Payment DTOs still expose monetary values as primitives

`Application/DTOs/PaymentDtos.cs`, `Application/Interfaces/ITransaction.cs`, and the published
`Client/EscrowDto.cs` expose monetary values as `decimal` or `long`. These shapes predate the shared
`Money` value object and force callers to infer or obtain currency separately. Persistence columns,
Stripe metadata, and calculator-local minor-unit arithmetic are intentional representations and are
not part of this debt.

**Resolves when:** every in-process and published Payment DTO uses `Money` for monetary values, with
conversion to minor units confined to persistence, provider, and protobuf mapper boundaries.

### A crashed two-phase refund can strand a `Pending` `PaymentRefundEntity` with no reconcile

Refunds now reserve → charge Stripe → complete: `EscrowService.ExecuteRefundAsync` and `ManagerPaymentService.RefundCommissionAuthorizedByBookingIdAsync` first commit a `Pending` `PaymentRefundEntity` (which bumps the aggregate `ConcurrencyToken`), then call Stripe, then transition the row `Pending → Completed` (on success) or `Pending → Failed` (on Stripe failure). If the process crashes *after* the reservation commits but *before* the completion/release save, the row is left `Pending` forever. This is **fail-closed**: a `Pending` row still `CountsTowardCumulative`, so it blocks (never double-charges) subsequent refunds up to its reserved gross — a naive retry of the same amount trips the cumulative-gross limit rather than issuing a second Stripe refund. The reservation gate is the only guard: the Stripe idempotency key is now keyed on the reservation's own id (`<scope>:<identity>:<reservationId>:1:refund`), so a fresh reservation is deliberately a fresh Stripe request rather than a replay of the stranded one. The reserved capacity stays locked until something clears the dangling row. There is no reconcile job that inspects Stripe for a `Pending` reservation and drives it to its true terminal state.

### Legacy Stripe writes key their idempotency on a single-attempt identity

Every Stripe write now builds its idempotency key through one `StripeIdempotencyKey` shape
(`<scope>:<identity>:<attempt>:<revision>:<action>`), and no key contains a payload field. Only the
payment-session subsystem supplies a genuine multi-attempt identity, because only it persists
`PaymentSessionAttemptEntity` rows; refunds supply their `PaymentRefundEntity` reservation id. The
remaining legacy charge, deposit, capture, release and hold-session writes are single-attempt by
construction — their durable row (`FinancialOperationEntity`, `SettlementTransactionEntity`,
`EscrowEntity`) short-circuits before a second provider write — so they pass their operation or
commission-binding id as their own attempt at revision 1. That is honest today but means a deliberate
second provider attempt against one of those identities cannot be expressed.

**Resolves when:** the legacy raw-identifier surface is culled
(`plans/launch/PAYMENT_BOUNDARY_DECISION.md` §7 step 5) and those flows move onto the payment-session
subsystem, which already carries attempt and revision.

**Resolves when:** a reconcile path exists — e.g. a background sweep (or webhook handler) that, for a `Pending` `PaymentRefundEntity` older than some threshold, queries Stripe for a refund under the reservation's idempotency key and either `Complete`s it (Stripe refund exists) or `Fail`s it (none), freeing the reserved gross.

### `PayoutAccountEntity.MarkVerified()` is production-dead

`Payment/src/Concertable.Payment.Domain/Entities/PayoutAccountEntity.cs` — `MarkVerified()` sets
`Status = PayoutAccountStatus.Verified`, but nothing in production ever calls it; the only caller is
`PaymentTestSeeder`. The live "is this account verified" read path (`PayoutAccountService.cs`,
`StripeAccountClient.cs`) queries Stripe directly instead of consulting this persisted column, so
`Status` only ever advances `NotVerified -> Pending` (via `LinkAccount`) in production, never reaching
`Verified`. Either the persisted status is meant to track Stripe's verification outcome (missing a
production caller — likely a webhook/reconciliation handler that never got wired) or the column/method
are vestigial from before verification checks moved to a live Stripe query.

**Resolves when:** either a production path calls `MarkVerified()` in response to the real verification
signal, or the method, the `Verified` status value, and any now-dead column plumbing are removed.

---

## RESOLVED

### ✅ `Payment.Seed.Contracts` parks consumer-domain data in Payment (agnostic-conduit violation)

Resolved by `plans/PAYMENT_SEED_REFLECTION_REFACTOR.md`. Rather than re-homing the seed-payment catalog onto the consumer side, the catalog and simulator were **deleted outright** — the cleaner outcome once it was clear Payment (an agnostic adapter that always runs) never needed a `*.Seed.Simulator` at all:

- `Concertable.Payment.Seed.Contracts` (the ticket-purchase catalog + `PaymentSeedSpec` incl. the 3 dead `Settlement`/`Escrow`/`Verify` factories) and `Concertable.Payment.Seed.Simulator` are gone, along with their AppHost wiring (`AddPaymentSeedingSimulator`, the resource-name constant, csproj/slnx entries).
- The only seed state those payments produced is **inherently-unreproducible historical state** (past-dated ticket sales). Each consumer now reflection-seeds its own copy: B2B sets `ConcertEntity.TicketsSold` via `ConcertFactory` from a `ticketsSold` field on `ConcertSeedSpec`; Customer direct-inserts `SeedState.Tickets` via `TicketDevSeeder`. Documented as a sanctioned exception in the `seeding` skill.
- `Payment.Contracts.PaymentSucceededEvent` stays — the only Payment-owned piece. Payment now owns **zero** ticket/concert knowledge.

---

## MEDIUM

### Stripe.net's API key is a global written from constructors, not an injected client

`StripeApiClient` and `StripeAccountClient` each assign `StripeConfiguration.ApiKey` in their own
constructor, and the E2E adapter's account client does it a third time. Every `Stripe.*Service` is
registered bare (`AddSingleton<Stripe.SetupIntentService>()`), so it resolves the key from that global when
a call is made rather than from a client it owns.

`AddPaymentInfrastructure` now assigns the key once at composition, which removes the ordering hazard that
made the first payment session of a process fail with `No API key provided`. The underlying shape is still
wrong: process-wide mutable state, three writers, and services that cannot be constructed with a different
key — so a test cannot exercise two keys, and the failure mode when someone adds a fourth writer is silent.

**Resolves when:** an `IStripeClient` is registered from `StripeSettings` and every `Stripe.*Service` is
constructed with it, no code assigns `StripeConfiguration.ApiKey`, and the E2E adapter overrides that one
registration instead of racing a global.
