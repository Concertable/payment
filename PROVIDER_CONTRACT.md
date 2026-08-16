# Payment provider contract

This document is the provider-neutral baseline for every Stripe-backed operation owned by
Concertable.Payment. It fixes the vocabulary, identities, provider products, legal state changes,
retry rules, Connect posture, and ownership boundaries that later persistence, webhook,
reconciliation, and frontend work must implement.

The machine-checked call-site inventory is
[`provider-contract-inventory.json`](provider-contract-inventory.json). The Payment unit architecture
test scans every declared root and fails when a provider call, consumer Payment-client call, frontend
confirmation, or client-secret parser is added or removed without an inventory decision.

## Provider and schema baseline

- `Stripe.net` is pinned to `47.3.0`. That SDK sends requests using Stripe API version
  `2025-01-27.acacia`; the repository does not override `Stripe-Version`.
- Webhook payload shape is controlled by the endpoint or account API version, not by the SDK version
  used for outgoing requests. Deserialization and normalization therefore target an explicit webhook
  version and fail closed on unknown values.
- On 2026-08-16, a read-only Stripe API query with the configured test-mode key returned endpoint
  `we_1RCqowQ1mmqr287N9MeY0iRV` for
  `https://concertable-app.azurewebsites.net/api/webhook`, with API version
  `2025-01-27.acacia`, `livemode=false`, and `status=disabled`. Its schema version matches the SDK
  request version. The available credential cannot inspect live-mode endpoints, so this evidence does
  not assert production endpoint state.
- Before a production endpoint processes this contract, deployment evidence must record its endpoint
  ID, mode, enabled status, and API version. A different version requires version-specific fixtures;
  changing the endpoint version is a separate deliberate migration.
- Stripe delivers webhooks at least once, can deliver duplicates, and does not guarantee ordering.
  Arrival order is never provider truth.

## Entry-point inventory

The checked-in JSON contains the exact source matches and their complete decisions. These are the
human-level entry points those matches implement.

| Owner | Entry point | Provider product or action | Current contract decision |
| --- | --- | --- | --- |
| Payment account client | Customer provisioning | Customer create | Payment owns the provider customer ID for an opaque consumer owner. |
| Payment account client | Connected-account provisioning, onboarding, and status | Express Account and AccountLink | Payment owns provider calls; B2B owns onboarding workflow. |
| Payment payout client | Payout setup and saved-card lookup | SetupIntent and PaymentMethod list | Saves a method only; setup success is not money movement. |
| Payment customer client | Ticket session creation | PaymentIntent and CustomerSession | Automatic capture, on-session, with later consumer-owned recovery. |
| Payment manager client | Saved-method payment | PaymentIntent | Server-confirmed on-session or consented off-session payment. |
| Payment manager client | Venue Hire setup | SetupIntent | Saves the artist method for declared off-session reuse. |
| Payment manager client | Door Split or Versus verification | SetupIntent | Verifies and saves the venue method without charging it. |
| Payment manager client | Flat Fee hold and hold recovery | Manual-capture PaymentIntent | Authorization becomes `Authorized` and carries `capture_before`. |
| Payment escrow client | Venue Hire deposit | PaymentIntent | Off-session server charge with explicit operation idempotency. |
| Payment escrow client | Flat Fee capture | PaymentIntent capture | Captures a known authorized attempt; new code never searches a latest-ten list. |
| Payment escrow client | Release | Transfer | Financial operation from the original source transaction, not a client session. |
| Payment escrow and manager clients | Refund | Refund and optional TransferReversal | Refund status is independent and follows the original Connect charge model. |
| Payment manager client | Door Split or Versus settlement | PaymentIntent | Off-session server charge following the typed deal calculation. |
| Payment webhook ingress | Signature validation, deduplication, and typed dispatch | Event with PaymentIntent or SetupIntent data | Current provider object truth and persisted revision govern mutation. |
| Customer API | Ticket checkout and saved-card purchase | Payment client session or server payment | Customer owns purchase attempt, validation, fulfillment, and durable query state. |
| Customer web | Ticket confirmation and next action | PaymentIntent | Current SignalR/client-secret bridge is a finite compatibility island. |
| Customer mobile | PaymentSheet ticket confirmation | PaymentIntent | Current intent-ID parsing and SignalR wait are a finite compatibility island. |
| B2B API | Hold, setup, verify, deposit, capture, refund, release, settlement | Payment client operations | Deal strategies own sequencing; Payment remains deal-type agnostic. |
| B2B web and shared web checkout | PaymentIntent or SetupIntent confirmation | Stripe.js confirmation | Current secret-prefix inference is finite compatibility, not target identity. |

No frontend calls Stripe's server API directly. There is no B2B mobile payment flow. The inventory
scans the empty B2B-web confirmation surface as well as populated surfaces so either fact changing
requires a decision.

## Provider-product matrix

Current flows stay on lower-level intents. Checkout Sessions do not own these lifecycles and are not
selected.

| Flow | Product | Presence and capture | Connect posture |
| --- | --- | --- | --- |
| Customer ticket checkout | PaymentIntent | On-session, automatic capture | Venue connected account is seller and settlement merchant; use `on_behalf_of` and destination settlement, retaining only the platform fee. |
| Customer saved-card payment | PaymentIntent | On-session unless recorded consent makes a genuinely absent customer off-session | Same charge posture as ticket checkout. |
| Flat Fee authorization | PaymentIntent | On-session, `capture_method=manual` | Artist connected account is settlement merchant and payee. |
| Venue Hire card save | SetupIntent | On-session setup, `usage=off_session` | No money movement; consent covers later deposit. |
| Door Split or Versus verification | SetupIntent | On-session setup, `usage=off_session` | No money movement; success only verifies reuse. |
| Venue Hire deposit | PaymentIntent | Off-session, server-confirmed automatic capture | Charge on behalf of the venue account; retain escrow ownership separately, then transfer on release. |
| Door Split or Versus settlement | PaymentIntent | Off-session, server-confirmed automatic capture | Charge and transfer direction follow the typed calculation; only platform fee belongs to Concertable. |
| Payout card setup | SetupIntent | Save only | Platform customer; never represent setup as a charge. |
| Capture | PaymentIntent capture | Server action before `capture_before` | Capture the known current attempt. |
| Refund | Refund | Server action with independent lifecycle | Reverse destination transfer or create a transfer reversal according to the original charge. |

Checkout may be reconsidered only for a new flow whose lifecycle is intentionally owned by Checkout.
That requires a baseline revision covering both its completion and payment-status axes.

## Identity and immutable binding

`OperationId` is a caller-generated UUIDv7. The consumer creates and persists it before the first
Payment request. It identifies one logical provider operation, not a booking, order, HTTP request,
SignalR subscription, Stripe object, or attempt.

`AttemptId` is a Payment-generated UUIDv7 for one provider-object attempt. An operation has exactly
one current attempt. A later attempt exists only after an explicit permitted retry and increments the
operation's monotonic revision.

Before provider creation, Payment hashes a canonical versioned request containing:

- operation kind and session kind;
- amount in integer minor units and ISO currency when money moves;
- capture mode and customer-presence mode;
- Payment-owned customer and connected-account bindings;
- opaque consumer owner key and applicable Connect charge model.

Presentation metadata and consumer-supplied raw provider IDs are excluded. The canonical encoding
and hash version are persisted with the operation.

| Request | Outcome |
| --- | --- |
| Same `OperationId`, same fingerprint | Replay: return the existing operation and current attempt. |
| Same `OperationId`, different fingerprint | `OperationConflict`; never mutate or create a provider object. |
| Changed amount, currency, owner, charge model, or session kind | Caller creates a new `OperationId`. |
| Transport retry or duplicate webhook | Never creates a revision. |
| Explicit retry of an eligible unchanged operation | Create next revision and new `AttemptId`; derive new action idempotency keys. |

Stripe idempotency keys are Payment-owned and derive from operation ID, attempt ID, revision, and the
specific action. Consumers cannot supply them.

## Session kinds

| Kind | Provider product | Success meaning |
| --- | --- | --- |
| `Payment` | Automatic-capture PaymentIntent | Funds captured or irrevocably entering provider processing. |
| `Authorization` | Manual-capture PaymentIntent | `requires_capture` normalized as `Authorized`; funds are held, not captured. |
| `PaymentMethodSetup` | SetupIntent | Method saved for declared future use with consent. |
| `PaymentMethodVerification` | SetupIntent | Method authenticated or verified without a charge. |

Capture, deposit, settlement charge, transfer, and refund are financial operations, not client
session kinds. Existing financial-operation commands and outcomes remain authoritative.

## Normalized states

The closed state vocabulary is `Creating`, `RequiresPaymentMethod`, `RequiresConfirmation`,
`RequiresAction`, `Processing`, `Authorized`, `Succeeded`, `Canceled`, and `Failed`.

### Provider status normalization

| Provider object | Provider status | Normalized state | Constraint |
| --- | --- | --- | --- |
| PaymentIntent | `requires_payment_method` | `RequiresPaymentMethod` | A `payment_failed` event does not override this recoverable current-object truth. |
| PaymentIntent | `requires_confirmation` | `RequiresConfirmation` | Confirmation is still required. |
| PaymentIntent | `requires_action` | `RequiresAction` | Consumer action is required. |
| PaymentIntent | `processing` | `Processing` | Reconciliation remains authoritative. |
| PaymentIntent | `requires_capture` | `Authorized` | Legal only for `Authorization`; persist provider `capture_before`. |
| PaymentIntent | `succeeded` | `Succeeded` | Terminal attempt. |
| PaymentIntent | `canceled` | `Canceled` | Terminal attempt; reason determines operation retryability. |
| SetupIntent | `requires_payment_method` | `RequiresPaymentMethod` | Recoverable setup failure. |
| SetupIntent | `requires_confirmation` | `RequiresConfirmation` | Confirmation is still required. |
| SetupIntent | `requires_action` | `RequiresAction` | Consumer action is required. |
| SetupIntent | `processing` | `Processing` | Reconciliation remains authoritative. |
| SetupIntent | `succeeded` | `Succeeded` | Terminal attempt; no money moved. |
| SetupIntent | `canceled` | `Canceled` | Terminal attempt. |
| Refund | `pending` | `Processing` | Refund remains independent of its original payment state. |
| Refund | `requires_action` | `RequiresAction` | Provider action flow is required. |
| Refund | `succeeded` | `Succeeded` | Terminal refund attempt. |
| Refund | `failed` | `Failed` | Terminal refund attempt. |
| Refund | `canceled` | `Canceled` | Terminal refund attempt. |

`Failed` is also the Concertable terminal state for an irrecoverable attempt failure for which no
recoverable provider-object status exists. An event type alone never manufactures it. Unknown status
strings, a SetupIntent observed for a payment kind, or `requires_capture` observed for a non-
authorization kind are typed normalization failures and do not mutate persisted state.

### Legal same-revision edges

Same-state observations are duplicate/no-op results after freshness and identity checks. Every other
edge not listed here is rejected and schedules current-object reconciliation.

| Current | Allowed next state |
| --- | --- |
| `Creating` | `RequiresPaymentMethod`, `RequiresConfirmation`, `RequiresAction`, `Processing`, `Authorized`, `Succeeded`, `Canceled`, `Failed` |
| `RequiresPaymentMethod` | `RequiresConfirmation`, `RequiresAction`, `Processing`, `Authorized`, `Succeeded`, `Canceled`, `Failed` |
| `RequiresConfirmation` | `RequiresPaymentMethod`, `RequiresAction`, `Processing`, `Authorized`, `Succeeded`, `Canceled`, `Failed` |
| `RequiresAction` | `RequiresPaymentMethod`, `RequiresConfirmation`, `Processing`, `Authorized`, `Succeeded`, `Canceled`, `Failed` |
| `Processing` | `RequiresPaymentMethod`, `RequiresAction`, `Succeeded`, `Canceled`, `Failed` |
| `Authorized` | `Processing`, `Succeeded`, `Canceled` |
| `Succeeded` | None; terminal protection. |
| `Canceled` | None; terminal protection. |
| `Failed` | None; terminal protection. |

Additional constraints narrow that table:

- `Authorized` is legal only for an authorization PaymentIntent.
- SetupIntent transitions never enter `Authorized`.
- Refund transitions use `Creating` to `Processing` or `RequiresAction`, then terminal state; they do
  not enter payment-method, confirmation, or authorization states.
- An observation must match operation, attempt, provider object, revision, and session kind.
- A lower revision is stale. A higher revision is unknown until its attempt exists locally. Neither
  mutates state.
- Provider timestamps are evidence, not a total order. If persisted evidence cannot prove freshness,
  reject the observation and fetch the current provider object.
- A terminal attempt cannot regress even when a later-delivered webhook carries an earlier state.

## Terminality, retry, revision, and expiry

- `Succeeded`, `Canceled`, and `Failed` are terminal for an attempt. `Authorized` is non-terminal.
- A succeeded operation is terminal. An explicit consumer cancellation is terminal for the operation.
- `RequiresPaymentMethod`, `RequiresConfirmation`, `RequiresAction`, and `Processing` continue the
  current attempt; they are not reasons to create a revision.
- Only an explicit consumer retry of an unchanged operation whose current attempt is `Failed` or
  expired may create a new revision. The retry uses the same fingerprint, a new `AttemptId`, and new
  action idempotency keys.
- Transport retry, timeout recovery, webhook redelivery, and reconciliation never create revisions.
- Authorization expiry uses Stripe's provider-reported `capture_before`. At or after that deadline,
  Payment reconciles the provider object; if it is still uncaptured, cancellation produces
  `Canceled` with safe reason `Expired`. A local timer alone does not claim provider truth.

## Safe public failures

Published failures contain only a closed code and a short Concertable-authored message.

| Code | Meaning | Retry disposition |
| --- | --- | --- |
| `PaymentMethodRequired` | A usable method is missing or the current method failed recoverably. | Continue current attempt with a method. |
| `AuthenticationRequired` | Consumer action is required. | Continue current attempt after action. |
| `Declined` | Provider declined the attempt. | Explicit retry may create a revision when the attempt is terminal. |
| `Expired` | Authorization passed its provider capture deadline. | Explicit unchanged-operation retry may create a revision. |
| `Canceled` | Consumer or provider canceled the operation. | Terminal unless the recorded reason is the retryable expiry case. |
| `OperationConflict` | Reused operation identity has a different immutable fingerprint. | Never retry with the same operation ID. |
| `ProviderUnavailable` | Provider could not be reached or its current truth is unavailable. | Reconcile or transport-retry the same attempt; never create a revision. |
| `Unknown` | No safe known classification applies. | Fail closed and reconcile. |

Stripe exception text, decline detail, request ID, client secret, and provider object ID stay in
Payment diagnostics. Transport mappings are exhaustive and fail closed on an unknown contract value.

## Truth and ownership

- Payment owns provider calls, provider IDs and secrets, normalization, attempts, current provider
  state, Connect mechanics, capture windows, idempotent mutation, webhook deduplication, and
  reconciliation.
- Customer owns ticket-purchase attempt state, inventory and price validation, fulfillment, and the
  customer-facing durable query.
- B2B owns deal and booking state, deal-strategy sequencing, and its financial saga.
- Frontends receive explicit session kind and safe operation snapshot. SignalR may accelerate an
  update, but consumer API query recovery is authoritative. Frontends never infer identity or kind
  from a client secret in new code.
- Published Payment contracts contain opaque operation and attempt identities plus Payment vocabulary
  only. They never reference Stripe types, Customer or B2B runtime assemblies, consumer identifiers,
  or consumer workflow enums.

## Compatibility boundary

Phase 1 changes no runtime API. `CheckoutSession`, `PaymentOutcome`, `PaymentSession`, every existing
RPC, `PaymentSucceededEvent`, `PaymentFailedEvent`, and the existing capture/deposit/refund command
surface remain unchanged. PR #552 remains the external owner of B2B typed-result adoption and its
additive `RefundReasonCodes` contract.

The current Customer web/mobile secret parsing, SignalR correlation, 30-second wait, shared web
secret-prefix inference, and B2B latest-ten hold lookup are explicit finite allowlists in the
inventory. They may be removed by their owning migrations; no new occurrence is permitted.

## Primary provider references

- [PaymentIntent lifecycle](https://docs.stripe.com/payments/paymentintents/lifecycle)
- [PaymentIntent status vocabulary](https://docs.stripe.com/api/payment_intents/object)
- [SetupIntent status vocabulary](https://docs.stripe.com/api/setup_intents/object)
- [Refund status vocabulary](https://docs.stripe.com/api/refunds/object)
- [Webhook delivery and version behavior](https://docs.stripe.com/webhooks)
- [Connect charge models](https://docs.stripe.com/connect/charges)
- [Manual capture and authorization validity](https://docs.stripe.com/payments/place-a-hold-on-a-payment-method)
