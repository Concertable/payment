# Concertable.Payment

## Payout accounts — integration events only

Payout accounts are **never manually seeded**. They are provisioned exclusively as a reaction to integration events: `TenantCreatedHandler` provisions the operator's account per `TenantCreatedEvent` (keyed on the tenant/owner id), and `CustomerRegisteredHandler` provisions the customer's account on `CredentialRegisteredEvent`. There is no `PaymentDevSeeder` and there must never be one. If payout accounts are missing in E2E or dev, fix the event flow — don't add a seeder.
