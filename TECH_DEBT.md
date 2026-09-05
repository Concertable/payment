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

---

## RESOLVED

### ✅ `Payment.Seed.Contracts` parks consumer-domain data in Payment (agnostic-conduit violation)

Resolved by `plans/PAYMENT_SEED_REFLECTION_REFACTOR.md`. Rather than re-homing the seed-payment catalog onto the consumer side, the catalog and simulator were **deleted outright** — the cleaner outcome once it was clear Payment (an agnostic adapter that always runs) never needed a `*.Seed.Simulator` at all:

- `Concertable.Payment.Seed.Contracts` (the ticket-purchase catalog + `PaymentSeedSpec` incl. the 3 dead `Settlement`/`Escrow`/`Verify` factories) and `Concertable.Payment.Seed.Simulator` are gone, along with their AppHost wiring (`AddPaymentSeedingSimulator`, the resource-name constant, csproj/slnx entries).
- The only seed state those payments produced is **inherently-unreproducible historical state** (past-dated ticket sales). Each consumer now reflection-seeds its own copy: B2B sets `ConcertEntity.TicketsSold` via `ConcertFactory` from a `ticketsSold` field on `ConcertSeedSpec`; Customer direct-inserts `SeedState.Tickets` via `TicketDevSeeder`. Documented as a sanctioned exception in the `seeding` skill.
- `Payment.Contracts.PaymentSucceededEvent` stays — the only Payment-owned piece. Payment now owns **zero** ticket/concert knowledge.
