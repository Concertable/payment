# Code review — Refactor/PostgresAuthConsumerSweep

> **This file is a work order, not a discussion.** If you're handed this file, fix the open `[ ]`
> findings directly and report what changed. Tick each `[x]` as you land it. Pause only for a genuinely
> irreversible or ambiguous finding: record its durable disposition, take the safe path, and keep going.

**Review status:** `complete`
**Reviewed up to commit:** `HEAD`  `(2026-09-20)`
**Judgment:** `approved`

## Review pass — 2026-09-20 — composition

**Candidate base:** `8ac44e1e`
**Candidate branch:** `Refactor/PostgresAuthConsumerSweep`
**Candidate scope:** `all`
**Candidate paths:** `Directory.Packages.props`, `local/AppHost/AppHost.cs`,
`api/tests/Concertable.Payment.StartupTests/ResourceGraphTests.cs`,
`reviews/Refactor-PostgresAuthConsumerSweep.md` `(4 paths)`
**Work-order path:** `reviews/Refactor-PostgresAuthConsumerSweep.md`
**Work-order mode:** `new`
**Pass judgment:** `approved`

The plan ledger's Next Steps step 2, for Payment. Identical in shape to the Search slice landed alongside
it: the `sql` container existed only to host `AuthDb` for a pre-cut-over `auth` image, and Auth's
cut-over removed the reason for it.

### Findings

No findings.

### Verified

- **The platform bump is forced.** `Concertable.Auth.Hosting 0.2.0-alpha.0.305` declares dependencies on
  `Concertable.AppHost.Shared 0.2.0-alpha.0.14` and `Concertable.Shared.Email.Application 0.2.0-alpha.0.14`
  (read from the published nuspec), so central package management cannot hold the platform at `0.13`
  beside it. Platform `0.14` is platform-dotnet #12, "Remove ambient tenant host bypass" — Payment
  references none of `ITenantContext`, `TenantInterceptor` or `ITenantScoped`, so the bump is inert here.
- Both auth digests were resolved from GHCR **by the exact commit tag** `0f80b89c...`, not by recency:
  `auth` `sha256:cbd7c429...`, `auth-migrations` `sha256:090b1bb8...`.
- `AddAuth` at `0.305` wires `WaitForCompletion(migrations)` itself, so the AppHost adds no trailing
  `WaitForCompletion` for auth — unlike Payment's own migrations resource two lines below, which still
  orders at the call site. That asymmetry is deliberate and is the ledger's open question for a later
  slice, not something this one changes.
- `AuthDb` moves onto the existing `concertable-payment-postgres-data` server. Payment's server has
  neither PostGIS nor `max_prepared_transactions`; Auth needs neither, matching Auth's own AppHost, which
  runs a plain `AddPostgresContainer`.
- The auth resource's `--user root` argument, its endpoint named `https` and the developer-certificate
  bridge are unchanged, and `ResourceGraphTests` still pins all three.
- `local/AppHost/AppHost.cs` was the only file in the repository naming `AddSqlServerContainer`,
  `AuthConstants.Database` or `AddAuth`.

### Test change and its limit

`ResourceGraphTests.ProductionGraphAndStrictValidation_AreValid` gains the same four assertions the Search
slice added: `AuthDb` is a `PostgresDatabaseResource`, `auth-migrations` waits on it until healthy, `auth`
waits for that job's completion, and no `SqlServerServerResource` exists in the graph.

**Stated plainly: Payment has no local suite that starts this composition.** `ResourceGraphTests` builds
the app model without running containers, and `E2EAdmin.IntegrationTests` (7/7 in about a second) does not
boot the real images. So this repository cannot prove the published `auth` and `auth-migrations` images
actually run here. Two things do cover it: the Search slice's E2E run, which boots the identical image
pair against a PostgreSQL `AuthDb` in a consumer composition, and System's Qualify at `a8066aec`, which
runs the same pair composed by digest. The gap is a property of Payment's test estate, not of this change.

### Gates

Release build 0 warnings / 0 errors. Startup 10/10, architecture 11/11, unit 522/522, integration 75/75,
E2E admin 7/7.
