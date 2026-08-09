# Concertable.Payment.Seed

## `StripeE2EAccountResolver` must cover every seeded user with a Stripe customer

`StripeE2ERun` must create a customer for every seeded manager (`SeedUsers.Managers`), plus every seeded customer that has saved test cards. `StripeE2EAccountResolver` consumes that supplied map generically; do not duplicate the user list there. Each E2E fixture creates distinct test-mode customers and supplies their IDs through `E2EStripe:Customers`; never restore fixed shared customer IDs. Connect account IDs remain fixed because tests do not mutate them.

If you find the resolver doesn't cover all of them, fill it in. **Don't reason around it.** A missing entry is a bug here, not a feature.

## `[payment].[PayoutAccounts]` row count is not an event-delivery count

`E2EStripeAccountClient.Provision*Async` calls `resolver.TryGet*` and returns silently on a miss — so a `CredentialRegisteredEvent` for a manager not in the resolver produces zero `PayoutAccount` rows even though the event was delivered and the handler ran end-to-end.

Don't use `PayoutAccounts` row counts to argue about whether the event pipeline is healthy. To check actual delivery, query `[messaging].[Inbox]` in `PaymentDb` filtered by `ConsumerName` — that row is written at the top of the handler before any short-circuit.
