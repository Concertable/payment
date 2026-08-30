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

### Internal Payment DTOs still expose monetary values as primitives

`Application/DTOs/PaymentDtos.cs`, `Application/Interfaces/ITransaction.cs`, and the published
`Client/EscrowDto.cs` expose monetary values as `decimal` or `long`. These shapes predate the shared
`Money` value object and force callers to infer or obtain currency separately. Persistence columns,
Stripe metadata, and calculator-local minor-unit arithmetic are intentional representations and are
not part of this debt.

**Resolves when:** every in-process and published Payment DTO uses `Money` for monetary values, with
conversion to minor units confined to persistence, provider, and protobuf mapper boundaries.

### A crashed two-phase refund can strand a `Pending` `PaymentRefundEntity` with no reconcile

Refunds now reserve → charge Stripe → complete: `EscrowService.ExecuteRefundAsync` and `ManagerPaymentService.RefundCommissionAuthorizedByBookingIdAsync` first commit a `Pending` `PaymentRefundEntity` (which bumps the aggregate `ConcurrencyToken`), then call Stripe, then transition the row `Pending → Completed` (on success) or `Pending → Failed` (on Stripe failure). If the process crashes *after* the reservation commits but *before* the completion/release save, the row is left `Pending` forever. This is **fail-closed**: a `Pending` row still `CountsTowardCumulative`, so it blocks (never double-charges) subsequent refunds up to its reserved gross — a naive retry of the same amount trips the cumulative-gross limit rather than issuing a second Stripe refund, and the Stripe idempotency key (`commission:{authId}:refund:{cumulativeGross}`) would collapse a same-amount retry onto the same Stripe refund anyway. But the reserved capacity stays locked until something clears the dangling row. There is no reconcile job that inspects Stripe for a `Pending` reservation and drives it to its true terminal state.

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

### gRPC mappers use the `""` literal and erase value presence

`Grpc/PaymentMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`, `TransactionId = r.TransactionId ?? ""`) and `Grpc/EscrowMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`). Proto3 strings can't be null, so a fallback at the wire boundary is genuinely required — but the `""` literal violates the `csharp-style` skill (`string.Empty` for semantic fallbacks), and the receiver has to interpret empty string as "absent" (e.g. no client secret when `RequiresAction` is false).

**Resolves when:** the literals become `string.Empty` at minimum; ideally the proto fields become `optional string` so presence survives the wire and callers test `Has*` instead of empty-string sentinels.

---

## RESOLVED

### ✅ `Payment.Seed.Contracts` parks consumer-domain data in Payment (agnostic-conduit violation)

Resolved by `plans/PAYMENT_SEED_REFLECTION_REFACTOR.md`. Rather than re-homing the seed-payment catalog onto the consumer side, the catalog and simulator were **deleted outright** — the cleaner outcome once it was clear Payment (an agnostic adapter that always runs) never needed a `*.Seed.Simulator` at all:

- `Concertable.Payment.Seed.Contracts` (the ticket-purchase catalog + `PaymentSeedSpec` incl. the 3 dead `Settlement`/`Escrow`/`Verify` factories) and `Concertable.Payment.Seed.Simulator` are gone, along with their AppHost wiring (`AddPaymentSeedingSimulator`, the resource-name constant, csproj/slnx entries).
- The only seed state those payments produced is **inherently-unreproducible historical state** (past-dated ticket sales). Each consumer now reflection-seeds its own copy: B2B sets `ConcertEntity.TicketsSold` via `ConcertFactory` from a `ticketsSold` field on `ConcertSeedSpec`; Customer direct-inserts `SeedState.Tickets` via `TicketDevSeeder`. Documented as a sanctioned exception in the `seeding` skill.
- `Payment.Contracts.PaymentSucceededEvent` stays — the only Payment-owned piece. Payment now owns **zero** ticket/concert knowledge.
