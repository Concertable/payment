# Concertable.Payment

## Payout accounts — integration events only

Payout accounts are **never manually seeded**. They are provisioned exclusively as a reaction to integration events: `PayoutOwnerRegisteredHandler` provisions the operator's account per `PayoutOwnerRegisteredEvent` (a Payment-owned event, keyed on the opaque owner id — published by B2B's Tenant module, but Payment never compile-depends on B2B's contracts), and `CustomerRegisteredHandler` provisions the customer's account on `CredentialRegisteredEvent`. There is no `PaymentDevSeeder` and there must never be one. If payout accounts are missing in E2E or dev, fix the event flow — don't add a seeder.
