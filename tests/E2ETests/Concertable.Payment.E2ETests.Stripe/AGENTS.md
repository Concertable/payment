# Concertable.Payment.E2ETests.Stripe

Payment-owned E2E host support: the Stripe account/webhook adapter (`StripeAccountClient`,
`StripeWebhookProcessor`, `StripeAccountResolver`) and the `UseStripeAdapter()` composition seam.
Deliberately light (Payment.Infrastructure only, no Aspire/Playwright) so the Payment E2E host
projects can reference it without dragging test tooling into a launched service process.

## `StripeAccountResolver` consumes only opaque owner mappings

Consumer E2E composition decides which seeded identities need provider customers and maps them to opaque owner IDs before configuring Payment. `StripeAccountResolver` consumes that supplied map generically and must not depend on consumer seed catalogs, roles, or tenant concepts. Each E2E fixture creates distinct test-mode customers and supplies their IDs through `E2EStripe:Customers`; never restore fixed shared customer IDs. Connect account IDs remain fixed because tests do not mutate them.

## `[payment].[PayoutAccounts]` row count is not an event-delivery count

`StripeAccountClient.Provision*Async` calls `resolver.Resolve*` and returns silently on a miss, so a Payment-owned owner-registration event with no configured mapping produces zero `PayoutAccount` rows even though the event was delivered and the handler ran end-to-end. To check actual delivery, query `[messaging].[Inbox]` in `PaymentDb` filtered by `ConsumerName` — that row is written at the top of the handler before any short-circuit.
