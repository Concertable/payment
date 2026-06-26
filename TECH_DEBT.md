# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## MEDIUM

### Payment compile-depends on `B2B.Tenant.Contracts` (a reverse adapter→data-service edge)

`Payment.Infrastructure` references `Concertable.B2B.Tenant.Contracts` purely for `TenantCreatedEvent`, which `TenantCreatedHandler` consumes to provision a payout account (treating `TenantId` as an opaque owner key). This is the wrong dependency direction — Payment is an agnostic **adapter**; it shouldn't compile-depend on a **data service**'s contracts (the `PAYMENT_AGNOSTIC_AUDIT` killed the other Payment→B2B edges, and the Phase 0 note in `plans/SERVICE_BUILD_SEPARATION.md` flagged this one as a regression that postdated it). Phase 3 deliberately **packaged** the edge (option a — `B2B.Tenant.Contracts` is now a `PackageReference`, not a `ProjectReference`) rather than re-routing it, because re-routing is a runtime change to the E2E-covered payout flow that belongs with the Phase 5 B2B work, not the build-separation packaging step. `TenantCreatedEvent` is consumed by nobody but Payment, so the re-route is clean when it happens.

**Resolves when:** the subscription is re-routed to a Payment-owned/generic event (the audit's "pattern E") — define e.g. `PayoutOwnerRegisteredEvent` in `Payment.Contracts`, have B2B's Tenant module publish it (a correct data→adapter edge), drop Payment's `B2B.Tenant.Contracts` reference. Needs an E2E run (payout-provisioning flow).

---

## LOW

### Missing Stripe webhook secret masked until the first webhook arrives

`WebhookService`'s constructor takes `stripeSettings.Value.WebhookSecret ?? string.Empty`. A missing secret should fail at startup; instead the service boots and every webhook fails signature verification at request time, which reads as a Stripe-side problem rather than missing config.

**Resolves when:** the options registration validates `WebhookSecret` is present (`ValidateOnStart` / throw on bind), and the `?? string.Empty` fallback in `WebhookService` is removed.

---

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
