# Concertable.Payment.E2ETests.Stripe

Payment-owned E2E host support: the Stripe account/webhook adapter (`StripeAccountClient`,
`StripeWebhookProcessor`, `StripeAccountResolver`) and the `UseStripeAdapter()` composition seam.
Deliberately light (Payment.Infrastructure only, no Aspire/Playwright) so the Payment E2E host
projects can reference it without dragging test tooling into a launched service process.

## `StripeAccountResolver` must cover every seeded user with a Stripe customer

`StripeCustomerResolver` must create a customer for every seeded manager (`SeedUsers.Managers`), plus every seeded customer that has saved test cards. `StripeAccountResolver` consumes that supplied map generically; do not duplicate the user list there. Each E2E fixture creates distinct test-mode customers and supplies their IDs through `E2EStripe:Customers`; never restore fixed shared customer IDs. Connect account IDs remain fixed because tests do not mutate them. A missing entry is a bug here, not a feature — fill it in, don't reason around it.

## `[payment].[PayoutAccounts]` row count is not an event-delivery count

`StripeAccountClient.Provision*Async` calls `resolver.Resolve*` and returns silently on a miss — so a `CredentialRegisteredEvent` for a manager not in the resolver produces zero `PayoutAccount` rows even though the event was delivered and the handler ran end-to-end. To check actual delivery, query `[messaging].[Inbox]` in `PaymentDb` filtered by `ConsumerName` — that row is written at the top of the handler before any short-circuit.
