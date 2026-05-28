# Concertable.Payment

## Payout accounts — integration events only

Payout accounts are **never manually seeded**. They are provisioned exclusively when `CredentialRegisteredEvent` fires on user registration, via `ManagerRegisteredHandler` (managers/artists) and `CustomerRegisteredHandler` (customers). There is no `PaymentDevSeeder` and there must never be one. If payout accounts are missing in E2E or dev, fix the event flow — don't add a seeder.
