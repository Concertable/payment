# Concertable.Payment

Adapter service — agnostic payment/ledger/escrow/payout, a shared runtime dependency of B2B + Customer (called over gRPC). Inherits root [`AGENTS.md`](../../AGENTS.md) + [`api/AGENTS.md`](../AGENTS.md); internal design → [`ARCHITECTURE.md`](./ARCHITECTURE.md) (read first, don't duplicate).

## Stay agnostic — a payment kind is a metadata `type`, never a domain concept

A new payment purpose is a new `type` string (`Contracts/TransactionTypes.cs`) + a keyed `ITransactionHandler`, never a ticket/concert/deal concept leaking into Payment. The resource `owner` is opaque: fail-closed `ICurrentPayoutOwner` at the HTTP boundary, explicit `owner_id` over gRPC.

## Never seed ledger, escrow, or payout rows

Payout accounts are provisioned **only** by handlers reacting to integration events — `PayoutOwnerRegisteredHandler` on `PayoutOwnerRegisteredEvent` (operator), `CustomerRegisteredHandler` on `CredentialRegisteredEvent` (buyer). There is no `PaymentDevSeeder` and never must be. Ledger/escrow rows are written only by the money flow. Missing rows in E2E/dev → fix the event flow, not a seeder.

## E2E/dev never touch real Stripe

`ExternalServices:UseRealStripe=false` (dev default) wires the `Fake*` clients; E2E layers `UseE2EStripeClient()` (pre-seeded test-mode IDs). Never add a path that calls live Stripe in dev/E2E.

## Money is `long` minor-units in the ledger

The ledger, all `*Minor` fields, and `CommissionCalculator` work in `long` minor units; `Money` (a major-unit decimal value object) converts at the edges via `To`/`FromMinorUnits()`. Never carry a major-unit decimal into ledger/escrow math.
