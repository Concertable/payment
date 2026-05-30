# Concertable.Payment — Technical Debt

When an item is fixed, update both this file and `ARCHITECTURE.md`.

---

## PaymentTestSeeder reaches into B2B and Customer internals

`Concertable.Payment.Infrastructure` has `ProjectReference`s to
`Concertable.B2B.Seed.Infrastructure` and `Concertable.Customer.Seed`, and
`Data/Seeders/PaymentTestSeeder.cs` injects `Concertable.B2B.Seed.Infrastructure.SeedData`
(aliased `B2BSeedData`) and `Concertable.Customer.Seed.SeedData` (`CustomerSeedData`).

This crosses service boundaries — Payment compiles against B2B's internal Domain
(via `SeedData`) and Customer's seed data. In the microservice model Payment must
only depend on cross-service `*.Contracts`, never another service's Domain.

It is test-only (an `ITestSeeder` used by integration tests), so the blast radius
is limited to the test build, but it still couples the Payment test assembly to B2B
and Customer internals and would break a clean repo split.

**Fix direction:** drive the integration-test payout/settlement state from
Payment-owned contracts or published events (the same path production uses), instead
of importing the other services' `SeedData`. Then drop the two `ProjectReference`s.
