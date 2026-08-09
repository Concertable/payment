# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## MEDIUM

### Production hosts compile against Payment's E2E-only Stripe adapter

`Concertable.Payment.Web` and `Concertable.Payment.Workers` both project-reference
`src/Seed/Concertable.Payment.Seed`, import `Concertable.Payment.Seed`, and branch on the `E2E`
environment to call `UseE2EStripeClient()`. That project contains `E2EStripeAccountClient`, its DI
replacement, and `StripeE2EAccountResolver` with hard-coded Stripe test-fixture IDs. The adapter's
behaviour is necessary—the browser tests need real Stripe test-mode intents while linking stable,
pre-provisioned accounts—but the dependency direction is inverted: deployable production projects
compile and ship test-only code so the tests can alter their composition.

`Payment.Seed` is also the wrong ownership label. This is Payment-owned E2E host support, not service
seed data; the production service must not acquire an E2E dependency merely because the replacement
needs access to Payment internals.

**Resolves when:**

- Extract reusable Web and Workers bootstrap seams plus the production-neutral ports needed to
  replace Stripe behaviour, so production entry points compose the ordinary Payment hosts while
  Payment-owned E2E host projects apply test replacements without duplicating startup or requiring
  friendship from a production assembly.
- Move `E2EStripeAccountClient`, `StripeE2EAccountResolver`, and their registration into
  `Concertable.Payment.E2ETests.Helpers` (or dedicated Payment E2E host projects), with test projects
  referencing production assemblies and never the reverse.
- Remove the `Concertable.Payment.Seed` references/usings, `UseE2EStripeClient()` environment branches,
  E2E-specific configuration, and `InternalsVisibleTo` entries naming Seed/E2E assemblies from
  Payment's production project closure; `api/Concertable.Payment/src` then contains no E2E-specific
  code or configuration.

---

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

### gRPC mappers use the `""` literal and erase value presence

`Grpc/PaymentMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`, `TransactionId = r.TransactionId ?? ""`) and `Grpc/EscrowMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`). Proto3 strings can't be null, so a fallback at the wire boundary is genuinely required — but the `""` literal violates `agents/CODE_CONVENTIONS.md` (`string.Empty` for semantic fallbacks), and the receiver has to interpret empty string as "absent" (e.g. no client secret when `RequiresAction` is false).

**Resolves when:** the literals become `string.Empty` at minimum; ideally the proto fields become `optional string` so presence survives the wire and callers test `Has*` instead of empty-string sentinels.

---

## RESOLVED

### ✅ `Payment.Seed.Contracts` parks consumer-domain data in Payment (agnostic-conduit violation)

Resolved by `plans/PAYMENT_SEED_REFLECTION_REFACTOR.md`. Rather than re-homing the seed-payment catalog onto the consumer side, the catalog and simulator were **deleted outright** — the cleaner outcome once it was clear Payment (an agnostic adapter that always runs) never needed a `*.Seed.Simulator` at all:

- `Concertable.Payment.Seed.Contracts` (the ticket-purchase catalog + `PaymentSeedSpec` incl. the 3 dead `Settlement`/`Escrow`/`Verify` factories) and `Concertable.Payment.Seed.Simulator` are gone, along with their AppHost wiring (`AddPaymentSeedingSimulator`, the resource-name constant, csproj/slnx entries).
- The only seed state those payments produced is **inherently-unreproducible historical state** (past-dated ticket sales). Each consumer now reflection-seeds its own copy: B2B sets `ConcertEntity.TicketsSold` via `ConcertFactory` from a `ticketsSold` field on `ConcertSeedSpec`; Customer direct-inserts `SeedState.Tickets` via `TicketDevSeeder`. Documented as a sanctioned exception in `agents/SEEDING_CONVENTIONS.md`.
- `Payment.Contracts.PaymentSucceededEvent` stays — the only Payment-owned piece. Payment now owns **zero** ticket/concert knowledge.
