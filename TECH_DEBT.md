# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## MEDIUM

### `EscrowService.RefundByBookingIdAsync` is asymmetric with `ReleaseByBookingIdAsync` — hard-fails on non-refundable escrow

`RefundByBookingIdAsync` (`EscrowService.cs`) only no-ops on already-`Refunded` escrow; for any other non-refundable status it delegates to `RefundAsync`, which **hard-fails** (`Result.Fail`) on `Pending`/`Failed`. Its sibling `ReleaseByBookingIdAsync` instead treats any non-`Held` escrow as a benign no-op (`Result.Ok(null)`) — the point of a `ByBookingId` convenience method being that a booking-lifecycle caller can invoke it blindly without knowing escrow state. The asymmetry means cancelling a booking whose escrow never advanced past `Pending` (hold initiated, webhook not yet confirmed) or is `Failed` fails the whole refund/cancel (gRPC `FailedPrecondition` → B2B `EscrowClient` `Result.Fail`) instead of no-op'ing. Flagged reviewing PR #76 (concert-cancel + escrow-refund) and not addressed before merge; whether it bites depends on how the B2B cancel handler treats a `FailedPrecondition` from refund.

**Resolves when:** the intended contract is decided and made symmetric — if "cancel is safe to call regardless of escrow state" (the Release precedent), `RefundByBookingIdAsync` treats `Pending`/`Failed` as `Result.Ok(null)` rather than propagating a hard failure.

---

## LOW

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
