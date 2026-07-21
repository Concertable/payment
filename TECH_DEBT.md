# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## MEDIUM

### Payment compile-depends on `B2B.Tenant.Contracts` (a reverse adapter→data-service edge)

`Payment.Infrastructure` references `Concertable.B2B.Tenant.Contracts` purely for `TenantCreatedEvent`, which `TenantCreatedHandler` consumes to provision a payout account (treating `TenantId` as an opaque owner key). This is the wrong dependency direction — Payment is an agnostic **adapter**; it shouldn't compile-depend on a **data service**'s contracts (the `PAYMENT_AGNOSTIC_AUDIT` killed the other Payment→B2B edges, and the Phase 0 note in `plans/SERVICE_BUILD_SEPARATION.md` flagged this one as a regression that postdated it). Phase 3's chosen fix is option (a) — **package** the edge (consume `B2B.Tenant.Contracts` as a `PackageReference`) rather than re-route it, because re-routing is a runtime change to the E2E-covered payout flow that belongs with the Phase 5 B2B work, not the build-separation packaging step. As of **Phase 3a** only the producer is published (`<IsPackable>true</IsPackable>` on `B2B.Tenant.Contracts`); the consumer edge is **still a `ProjectReference`** in `Concertable.Payment.Infrastructure.csproj` — the flip to `PackageReference` lands in **Phase 3b**. `TenantCreatedEvent` is consumed by nobody but Payment, so the re-route is clean when it happens.

**Resolves when:** the subscription is re-routed to a Payment-owned/generic event (the audit's "pattern E") — define e.g. `PayoutOwnerRegisteredEvent` in `Payment.Contracts`, have B2B's Tenant module publish it (a correct data→adapter edge), drop Payment's `B2B.Tenant.Contracts` reference. Needs an E2E run (payout-provisioning flow).

---

### `EscrowService.RefundByBookingIdAsync` is asymmetric with `ReleaseByBookingIdAsync` — hard-fails on non-refundable escrow

`RefundByBookingIdAsync` (`EscrowService.cs`) only no-ops on already-`Refunded` escrow; for any other non-refundable status it delegates to `RefundAsync`, which **hard-fails** (`Result.Fail`) on `Pending`/`Failed`. Its sibling `ReleaseByBookingIdAsync` instead treats any non-`Held` escrow as a benign no-op (`Result.Ok(null)`) — the point of a `ByBookingId` convenience method being that a booking-lifecycle caller can invoke it blindly without knowing escrow state. The asymmetry means cancelling a booking whose escrow never advanced past `Pending` (hold initiated, webhook not yet confirmed) or is `Failed` fails the whole refund/cancel (gRPC `FailedPrecondition` → B2B `EscrowClient` `Result.Fail`) instead of no-op'ing. Flagged reviewing PR #76 (concert-cancel + escrow-refund) and not addressed before merge; whether it bites depends on how the B2B cancel handler treats a `FailedPrecondition` from refund.

**Resolves when:** the intended contract is decided and made symmetric — if "cancel is safe to call regardless of escrow state" (the Release precedent), `RefundByBookingIdAsync` treats `Pending`/`Failed` as `Result.Ok(null)` rather than propagating a hard failure.

---

## LOW

### gRPC mappers use the `""` literal and erase value presence

`Grpc/PaymentMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`, `TransactionId = r.TransactionId ?? ""`) and `Grpc/EscrowMappers.cs` (`ClientSecret = r.ClientSecret ?? ""`). Proto3 strings can't be null, so a fallback at the wire boundary is genuinely required — but the `""` literal violates `docs/CODE_CONVENTIONS.md` (`string.Empty` for semantic fallbacks), and the receiver has to interpret empty string as "absent" (e.g. no client secret when `RequiresAction` is false).

**Resolves when:** the literals become `string.Empty` at minimum; ideally the proto fields become `optional string` so presence survives the wire and callers test `Has*` instead of empty-string sentinels.

---

## RESOLVED

### ✅ `Payment.Seed.Contracts` parks consumer-domain data in Payment (agnostic-conduit violation)

Resolved by `plans/PAYMENT_SEED_REFLECTION_REFACTOR.md`. Rather than re-homing the seed-payment catalog onto the consumer side, the catalog and simulator were **deleted outright** — the cleaner outcome once it was clear Payment (an agnostic adapter that always runs) never needed a `*.Seed.Simulator` at all:

- `Concertable.Payment.Seed.Contracts` (the ticket-purchase catalog + `PaymentSeedSpec` incl. the 3 dead `Settlement`/`Escrow`/`Verify` factories) and `Concertable.Payment.Seed.Simulator` are gone, along with their AppHost wiring (`AddPaymentSeedingSimulator`, the resource-name constant, csproj/slnx entries).
- The only seed state those payments produced is **inherently-unreproducible historical state** (past-dated ticket sales). Each consumer now reflection-seeds its own copy: B2B sets `ConcertEntity.TicketsSold` via `ConcertFactory` from a `ticketsSold` field on `ConcertSeedSpec`; Customer direct-inserts `SeedState.Tickets` via `TicketDevSeeder`. Documented as a sanctioned exception in `docs/SEEDING_CONVENTIONS.md`.
- `Payment.Contracts.PaymentSucceededEvent` stays — the only Payment-owned piece. Payment now owns **zero** ticket/concert knowledge.
