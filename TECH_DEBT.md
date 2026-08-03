# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## LOW

### A crashed two-phase refund can strand a `Pending` `PaymentRefundEntity` with no reconcile

Refunds now reserve → charge Stripe → complete: `EscrowService.ExecuteRefundAsync` and `ManagerPaymentService.RefundCommissionAuthorizedByBookingIdAsync` first commit a `Pending` `PaymentRefundEntity` (which bumps the aggregate `ConcurrencyToken`), then call Stripe, then transition the row `Pending → Completed` (on success) or `Pending → Failed` (on Stripe failure). If the process crashes *after* the reservation commits but *before* the completion/release save, the row is left `Pending` forever. This is **fail-closed**: a `Pending` row still `CountsTowardCumulative`, so it blocks (never double-charges) subsequent refunds up to its reserved gross — a naive retry of the same amount trips the cumulative-gross limit rather than issuing a second Stripe refund, and the Stripe idempotency key (`commission:{authId}:refund:{cumulativeGross}`) would collapse a same-amount retry onto the same Stripe refund anyway. But the reserved capacity stays locked until something clears the dangling row. There is no reconcile job that inspects Stripe for a `Pending` reservation and drives it to its true terminal state.

**Resolves when:** a reconcile path exists — e.g. a background sweep (or webhook handler) that, for a `Pending` `PaymentRefundEntity` older than some threshold, queries Stripe for a refund under the reservation's idempotency key and either `Complete`s it (Stripe refund exists) or `Fail`s it (none), freeing the reserved gross.

### Payment.Domain entities are uniformly `public`, violating the `internal`-default rule in `MODULAR_MONOLITH_RULES.md`

All 13 entities in `Concertable.Payment.Domain/Entities` (`EscrowEntity`, `SettlementTransactionEntity`, `TransactionEntity`, `PaymentRefundEntity`, `CommissionBindingEntity`, `CommissionConfigurationEntity`, ledger/payout/stripe entities …) are declared `public`, but `api/agents/MODULAR_MONOLITH_RULES.md` requires Domain entities to default to `internal` with tests using `InternalsVisibleTo`. Surfaced by the Feature/PricingTransparency review (CV1): the finding asked to make the new commission/refund entities `internal`, but doing so for only those would be inconsistent with every existing entity and would cascade compile breakage across Application/Infrastructure that reference them publicly. Deferred rather than singling out the new entities.

**Resolves when:** the rule is applied to the **entire** Payment.Domain entity surface as one deliberate refactor — make entities `internal`, add the Payment integration-test friend assembly, and re-promote only genuinely cross-module types back to `public` — or `MODULAR_MONOLITH_RULES.md` is amended if public entities are the accepted reality here.

### Concurrent *first-use* of a shared ledger account now relies on transaction-rollback + retry, not in-line reconcile

Migrating ledger posting to `IUnitOfWork<PaymentDbContext>.ExecuteAsync` removed `LedgerService`'s hand-rolled `CommitPostingAsync`/`ReconcileConcurrentAccountsAsync` loop, which used to catch the account `IdentityIndex` duplicate-key violation from two postings creating the same account concurrently (e.g. the very first two settlements both minting the singleton `PlatformRevenue` account) and reconcile them onto one row so both postings still committed. Under the staged design each posting commits in its own transaction, so on that race one commit wins and the loser's whole `ExecuteAsync` block (escrow/settlement mutation + ledger staging) rolls back. The async/message paths self-heal on retry (account then exists); a synchronous gRPC caller (`EscrowService` hold/release) would surface the failure to retry itself. Duplicate-*posting* idempotency is unaffected — still enforced by the `PostingIdentityIndex` unique index (tested in `LedgerTransactionConfigurationTests`) plus the `EscrowConfirmedHandler` status guard. The old `LedgerPostingConcurrencyTests` (which drove the deleted reconcile via a real-localdb save barrier) was removed with the migration.

**Resolves when:** the first-use race is decided — either accept transaction-rollback + retry as the contract (add an integration test asserting concurrent first-use converges after retry) or pre-provision the singleton platform accounts so no first-use race exists.

### Published `Payment.Client` metadata params are still `IDictionary`, not `IReadOnlyDictionary`

`ICustomerPaymentClient` / `IManagerPaymentClient` (and their `Adapters` impls) in the published
`Concertable.Payment.Client` package still take `IDictionary<string, string> metadata`. Nothing mutates
it — every read is read-only — so like the Payment-internal surface it should be `IReadOnlyDictionary`.
It was left out of that narrowing sweep because `Payment.Client` is consumed by B2B and Customer (and
their test fixtures *implement* the interfaces), so changing the signature is a breaking package change
that can't land in one PR — it needs an expand/contract across a platform-version bump.

**Resolves when:** the pair narrows to `IReadOnlyDictionary` via a breaking `Payment.Client` release +
the platform-sync bump that carries it to B2B/Customer.

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
