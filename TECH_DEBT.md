# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## LOW

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
