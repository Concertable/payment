# Concertable.Payment — Architecture

> Cross-service design rationale and decision history: the `microservices-architecture` skill
> Keyed-dispatch pattern: the `keyed-strategies` skill
> Provider lifecycle baseline: [`PROVIDER_CONTRACT.md`](./PROVIDER_CONTRACT.md)
> Outstanding gaps: [`TECH_DEBT.md`](./TECH_DEBT.md)

---

## Bounded context

Payment is the **agnostic payment adapter**: it owns the Stripe integration, a double-entry money ledger, the escrow lifecycle, the commission/VAT engine, and Stripe-Connect payout accounts. It is a shared runtime dependency of B2B and Customer (both `WaitFor` it), and it may be called synchronously over gRPC.

It knows **nothing** of tickets, concerts, deals, or reviews as domain concepts. A payment's purpose reaches Payment only as an opaque Stripe-metadata `type` string (`"ticket"`, `"settlement"`, `"escrow"`, `"verify"`), and the resource owner only as an opaque `owner` id. Payment owns zero consumer-domain knowledge (the former seed catalog + simulator were deleted; see `TECH_DEBT.md`), and it does not seed — payout accounts are event-provisioned (see [`AGENTS.md`](./AGENTS.md)).

---

## Host topology

| Project | Kind | Purpose |
|---|---|---|
| `Concertable.Payment.Web` | ASP.NET Core HTTP host | Controllers + gRPC server + outbox **dispatcher** + queue hosted service. **Publishes** `PaymentSucceeded/Failed`; handles the `ProcessStripeWebhookCommand`. |
| `Concertable.Payment.Workers` | .NET Worker host | ASB **subscribers** (`CredentialRegisteredEvent`, `PayoutOwnerRegisteredEvent`, `PaymentSucceeded/Failed`) + inbox. |
| `Concertable.Payment.Api` | Controllers csproj | HTTP controllers, `owner`-claim identity, gRPC **server** stubs. |
| `Concertable.Payment.Application` | Shared csproj | Interfaces, DTOs, requests, mappers, transaction-handler contracts. |
| `Concertable.Payment.Domain` | Shared csproj | Entities, enums, the pure `CommissionCalculator`. |
| `Concertable.Payment.Infrastructure` | Shared csproj | EF, services, Stripe clients, gRPC services, event handlers, webhook pipeline. |
| `Concertable.Payment.Client` | **Packable** package | Refit-free gRPC **client** stubs + typed adapters (`I*Client`). Consumed by B2B/Customer. |
| `Concertable.Payment.Contracts` | **Packable** package | Integration events + cross-service DTOs + metadata-key/`type` constants. |
| `Concertable.Payment.AppHost` | Aspire AppHost | Local-dev orchestrator only. |

**Database:** `PaymentDb` (SQL Server), single `PaymentDbContext`, default schema `payment` (table constants in `Infrastructure/Schema.cs`). Web migrates only when not Production; Workers migrates unconditionally (plus the outbox/inbox contexts).

---

## Double-entry ledger

Every money movement posts a balanced transaction — the invariant is enforced in the domain, not by convention:

- `LedgerTransactionEntity.Post(legs)` (`Domain/Entities/`) requires ≥2 legs sharing one currency and **throws unless the signed amounts sum to zero** (`"Ledger transaction does not balance"`).
- `LedgerEntryEntity` stores a leg as a signed `long` minor-unit amount (debit `+`, credit `−`); `LedgerAccountEntity` is keyed `(LedgerAccountType, Guid? OwnerId, Currency)` over accounts `PlatformRevenue / StripeClearing / Payable / Receivable / VatLiability`.
- **Posting recipes** — which accounts move per financial event — live in one place, `Infrastructure/LedgerPostings.cs` (`DirectSettlement`, `EscrowHold`, `EscrowRelease`, `EscrowRefundBeforeRelease`, `EscrowRefundAfterRelease`, `DirectSettlementRefund`), staged through `LedgerService.StageAsync` (resolves/creates accounts, builds the balanced transaction). The service stages entities then flushes `PaymentDbContext` **once** — the single-context unit-of-work (`persistence` skill).

**Transactions** are a TPH hierarchy: abstract `TransactionEntity` → `TicketTransactionEntity` / `SettlementTransactionEntity` / `VerifyTransactionEntity`, status `Pending/Complete/Failed`.

---

## Escrow — two-phase hold and two-phase refund

`EscrowEntity` (status `Pending → Held → Released` / `Refunded` / `Disputed` / `Failed`), driven by `EscrowService`:

- **Hold → release.** `DepositAsync` places a Stripe manual-capture hold, creates the escrow `Pending`, then `Confirm()` + stages `EscrowHold` when no further action is required. `CaptureAsync` captures a held intent; `ReleaseAsync`/`ReleaseByBookingIdAsync` transfers to the payee, `Release()`, stages `EscrowRelease`.
- **Reserve-first refund.** `ExecuteRefundAsync` atomically reserves against `EscrowEntity.RefundedGrossMinor` (`TryReserveRefundGrossAsync`), creates a **`Pending`** `PaymentRefundEntity`, saves, then calls Stripe — completing (`CompleteRefund` + posting) or rolling back (`ReleaseRefund` + release the reservation) on the result. The running `RefundedGrossMinor` total is the concurrency guard; the Stripe idempotency key collapses same-amount retries. The settlement path mirrors this via `ManagerPaymentService` + `TryReserveSettlementRefundGrossAsync`; a `PaymentRefundEntity` belongs to exactly one of escrow or settlement.

A crash between reservation and completion can strand a `Pending` refund (fail-closed but capacity-locked) — `TECH_DEBT.md` item.

---

## Commission / VAT engine

`CommissionCalculator` (`Domain/`, pure) computes commission from a payee-gross and basis-point rate (half-up rounding), then splits net/VAT out of the gross (`net = gross·10000/(10000+vatRate)`); GBP-only. `CommissionService` (`Infrastructure/`) wraps it with `Preview` / `CreateOrBind` / `CalculateBound`, pulling rate/VAT/fee from `Infrastructure/Settings/` options (`PlatformCommissionOptions`, `PlatformCommissionTaxOptions`, `PlatformFeeOptions`, each validated). A bound commission is persisted as a `CommissionBinding` so the rate a booking was priced at is frozen.

**`Money` is a major-unit value object** (`Kernel.ValueObjects.Money` — `decimal Amount` + `Currency`), used at the edges; the ledger, all `*Minor` fields, and the calculator work in `long` **minor units** via `Money.ToMinorUnits()` / `FromMinorUnits()`. Money crosses boundaries as minor-unit `long`s.

---

## Keyed transaction dispatch

A succeeded payment routes by its opaque metadata `type` (`Contracts/PaymentMetadataKeys.cs`, `Contracts/TransactionTypes.cs`):

`PaymentIntentWebhookHandler` publishes `PaymentSucceededEvent(intentId, metadata)` → `PaymentTransactionHandler` reads `metadata["type"]` → `ITransactionHandlerFactory.Create(type)` → the keyed `ITransactionHandler`:

| `type` | Handler |
|---|---|
| `ticket` | `TicketTransactionHandler` |
| `settlement` | `SettlementTransactionHandler` |
| `escrow` | `EscrowConfirmedHandler` |
| `verify` | `VerifyTransactionHandler` |

`PaymentFailedEvent` dispatches the same way via `PaymentFailureDispatcher` (`escrow` → `EscrowFailedHandler`, `settlement` → `SettlementFailedHandler`). This is the keyed-strategy shape, resolved through a keyed-service-locator factory rather than the canonical `FrozenDictionary` facade — a documented variant, not the pattern's default (`keyed-strategies` skill).

---

## Stripe seams — real vs fake

Every Stripe call sits behind an interface (`Application/Interfaces/`: `IStripeAccountClient`, `IStripeHoldClient`, `IStripePaymentIntentClient`, `IStripeTransferClient`, `Webhook/IStripeApiClient`, `IWebhookService`). Selection is by environment, never by touching real Stripe in dev/E2E:

- **`ExternalServices:UseRealStripe`** (bool) in `AddPaymentInfrastructure` — `false` (dev default) registers the `Fake*` clients; `true` registers the Stripe-SDK-backed real clients.
- **`UseStripeAdapter()`** (`tests/E2ETests/Concertable.Payment.E2ETests.Stripe`) layers on top of `UseRealStripe=true`, swapping `IStripeAccountClient` for the E2E `StripeAccountClient` and the webhook processor for `StripeWebhookProcessor`. It is applied only by the Payment E2E host projects (`Concertable.Payment.E2ETests.Web` / `.Workers`); the E2E stack launches those in place of the production hosts (via the harness's `LaunchAs` swap), so production `Payment.Web`/`Workers` carry no E2E branch. Each fixture creates its own **real test-mode** customers so concurrent runs cannot detach or reuse one another's cards; pre-provisioned Connect accounts remain shared because tests do not mutate them. The webhook processor accepts only intents owned by the fixture's customers.

---

## Webhook pipeline & idempotency

`WebhookController` (`POST api/Webhook`) reads the raw body + `Stripe-Signature` → `WebhookService` verifies the signature (`EventUtility.ValidateSignature`, secret from `StripeSettings`) and enqueues a `ProcessStripeWebhookCommand` through the outbox → `WebhookProcessor` applies the runtime resource-scope filter, then routes `PaymentIntent`/`SetupIntent` objects to their handlers. Production accepts the whole Stripe account; E2E accepts only intents for its run-scoped customers. Idempotency is two-layered:

1. **Stripe-event dedup** — `WebhookProcessor` skips if `StripeEventEntity` (keyed on Stripe event id, `[payment].[StripeEvents]`) already exists, else inserts it inside the same outbox transaction as the side-effects.
2. **Messaging inbox** — subscribers dedup on `(MessageId, ConsumerName)`.

Outbound Stripe calls carry idempotency keys (`Services/StripeIdempotency.cs`).

---

## Payout accounts — Stripe Connect Express, event-provisioned

`PayoutAccountEntity` (opaque `OwnerId`, `StripeAccountId`, `StripeCustomerId`, status `NotVerified/Pending/Verified`) is **never seeded** — it is provisioned by handlers (`Infrastructure/Handlers/`):

- `PayoutOwnerRegisteredHandler` ← `PayoutOwnerRegisteredEvent` (Payment-owned; published by B2B's Tenant module, keyed on the opaque owner id — no B2B compile dependency) → provisions a Stripe **Express** Connect account (`Type = "express"`, `Country = "GB"`, card-payments + transfers).
- `CustomerRegisteredHandler` ← `CredentialRegisteredEvent` → provisions the Stripe customer for buyer client ids only.

---

## gRPC surface & the `owner` boundary

The proto (`Client/Protos/payment.proto`) generates client stubs in `Payment.Client` and server stubs in `Payment.Api`; services are mapped with `RequireAuthorization("ServiceToken")` (`Infrastructure/Extensions/RoutingExtensions.cs`). Services: **`CommissionPricing`**, **`ManagerPayment`**, **`CustomerPayment`**, **`Escrow`**, **`PayoutAccount`**; typed adapters in `Client/Adapters/` (`ICommissionPricingClient`, `ICustomerPaymentOperationsClient`, `IManagerPaymentOperationsClient`, `IEscrowOperationsClient`, `IPayoutAccountOperationsClient`).

The opaque `owner` is resolved two different ways by design:

- **HTTP** — `ICurrentPayoutOwner` (`Api/Identity/`, namespace `Concertable.Payment.Api.Identity`) reads the `owner` claim off the principal and **fail-closes** (throws `UnauthorizedException` when absent). Used only by `StripeAccountController`, which Customer calls directly.
- **gRPC** — `PayoutAccount` RPCs take `owner_id` as an explicit request field; B2B passes its active tenant id (sourced from `ITenantContext`), never a claim. This is the "shared identity carries no owner concept" boundary from the `microservice-boundaries` skill.

---

## Integration events

| Direction | Event | Notes |
|---|---|---|
| Published | `PaymentSucceededEvent` `(TransactionId, Metadata)` | emitted by `PaymentIntentWebhookHandler`; the only carrier of "what this payment was for" (opaque metadata) |
| Published | `PaymentFailedEvent` `(TransactionId, FailureCode, FailureMessage, Metadata)` | |
| Consumed | `CredentialRegisteredEvent` (Auth) | provisions Stripe customer |
| Consumed | `PayoutOwnerRegisteredEvent` (Payment-owned) | provisions Express account |
| Consumed | `PaymentSucceededEvent` / `PaymentFailedEvent` (self) | Workers-side transaction/failure dispatch |

---

## Authentication

JWT Bearer; accepted audiences `concertable.payment.api` / `concertable.b2b.api` / `concertable.customer.api`. gRPC + write endpoints require policy `ServiceToken` (`scope=payment:write`). Callers obtain service tokens via `client_credentials`.

---

## Tech stack

.NET 10 · EF Core + SQL Server (`PaymentDbContext : DbContextBase`) · Stripe.net · gRPC (`Grpc.AspNetCore`, `Google.Protobuf`) · Azure Service Bus + `Concertable.Messaging` (Outbox/Inbox/Transport) · Aspire (`Concertable.ServiceDefaults`) · `Concertable.Shared.Api` · Dapper. Published client operations return Reunion results with Payment-owned error unions (`result-errors` skill).

---

## What is NOT in this service

| Concern | Lives in |
|---|---|
| What a payment is *for* (ticket, booking, deal) | the caller — Payment sees only opaque metadata `type` |
| Who an `owner` *is* (tenant, buyer) | `Concertable.B2B` / `Concertable.Customer` |
| Concert workflow, settlement obligations | `Concertable.B2B` |
| Ticket entities, customer profile | `Concertable.Customer` |
| Identity authority (`sub`, tokens, the `owner`/`role` claim split) | `Concertable.Auth` |
