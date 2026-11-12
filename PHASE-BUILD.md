# Task — Add managed investment portfolios to eShopOnWeb

Let eShopOnWeb shoppers hold a **managed investment portfolio**, with **Upvest** as the
investment provider. The shop's operator defines a small set of model portfolios — each one a
mix of funds with a target weight per fund. A shopper signs up as an investor, picks one model,
and pays money in; each payment is invested across the funds in their model's weights. When the
operator changes a model, or holdings have drifted from their targets, the operator realigns the
portfolios that follow it. A shopper can see what their portfolio holds and what it is worth.
eShopOnWeb today has no notion of an investor or of anything held on a shopper's behalf outside
the shop. This is an **additive** capability — it does not replace the existing
catalog/basket/order flow.

You own the design and every implementation decision — architecture, file layout, build order,
patterns. Just honor the mandates and the details below.

---

## What to build

### Flow 1 — Becoming an investor

- `POST /api/portfolio/investor` — sign the signed-in shopper up as an investor. The request
  carries the shop's investor sign-up form: `firstName`, `lastName`, `email`, `birthDate`
  (ISO-8601 date), `birthCity`, `birthCountry`, `nationality` (ISO 3166-1 alpha-2), `address`
  (`line1`, `postcode`, `city`, `country`), `phoneNumber`, `taxId`, `taxCountry`, and the
  identity document the shop checked when it verified the shopper: `identityDocument` (`type`,
  `number`, `issuingCountry`, `expiryDate`). The shopper becomes an investor with Upvest, with an
  account able to hold a managed portfolio.
- `GET /api/portfolio/investor` — where the caller's sign-up has got to.

A shopper can pay in only once Upvest has accepted them as an investor and their account is
ready. A shopper's investor record, payments and portfolio belong to that shopper alone: one
shopper must never see another's. Personal details are never written to logs.

### Flow 2 — Model portfolios

- `PUT /api/portfolio/models/{modelId}` — create or replace a model portfolio. The request carries
  `name` and `funds`: a list of `{ instrumentId, weight }`, where `instrumentId` is the fund's
  ISIN and `weight` a percentage. The weights of a model add up to exactly 100.
- `GET /api/portfolio/models` — every model portfolio and its funds.
- `PUT /api/portfolio/investor/model` — the signed-in investor chooses which model their portfolio
  follows (`modelId`). They may switch to another model later.

Changing a model's funds or weights does not by itself trade anything for the investors who
follow it; it takes effect for them at the next realignment (Flow 4).

### Flow 3 — Paying in

- `POST /api/portfolio/payments` — the signed-in investor pays an amount in (`amount`, in euros).
  The money is credited to their account at Upvest and invested across the funds of the model
  they follow, in that model's weights. A payment from a shopper who is not an accepted investor
  with a model chosen is refused.
- `GET /api/portfolio/payments` — the caller's payments, newest first, each showing the amount
  and where it has got to.

No real money moves in this integration: in Upvest's sandbox, money reaches an investor's account
through whatever Upvest provides for that.

### Flow 4 — Realignment

- `POST /api/portfolio/models/{modelId}/realignments` — bring the portfolio of every investor who
  follows that model back to the model's current weights.
- `GET /api/portfolio/realignments/{realignmentId}` — where that realignment has got to.

### Flow 5 — The investor's portfolio

- `GET /api/portfolio` — the caller's portfolio as Upvest currently records it: the model it
  follows, each fund held with its quantity and current value, the portfolio's total current
  value, and the total amount paid in.

### Status at Upvest

Every status this application reports — of a sign-up, a payment or a realignment — must reflect
what actually happened at Upvest. This application exposes no route for Upvest to call; it learns
what happened at Upvest through its own calls.

### Where it goes

Expose all capabilities as HTTP endpoints on the **`src/PublicApi`** project (JWT-authenticated;
the caller's identity comes from the token), following that project's existing endpoint
conventions, routed under `/api/` as named above. Every flow above has to be drivable through
that API alone, and each action a caller can take stays separately invocable. No storefront UI is
required.

The model-portfolio endpoints (`PUT /api/portfolio/models/{modelId}`, the realignment `POST`) are
**operator** actions: restrict them to the administrator role this project already uses for its
privileged endpoints. Every other endpoint above is for any signed-in shopper, acting for
themselves.

### Response fields

So the flows can be driven end to end by a caller, these names are fixed; everything else about
the response shape is your call.

- `POST` and `GET /api/portfolio/investor` return `investorId` and `status`, which is `pending`
  until Upvest has accepted the shopper and their account is ready, then `active` — or `rejected`
  if Upvest will not take them on.
- `GET /api/portfolio/models` returns a list whose entries carry `modelId`, `name` and `funds`
  (`instrumentId`, `weight`).
- `POST /api/portfolio/payments` returns `paymentId` and `status`. Each entry of
  `GET /api/portfolio/payments` carries `paymentId`, `amount` and `status`, which is `pending`
  until the payment's outcome at Upvest is known, then `invested` or `failed`.
- The realignment `POST` and `GET` return `realignmentId` and `status`, which is `pending` until
  the outcome at Upvest is known, then `completed` or `failed`.
- `GET /api/portfolio` returns `modelId`, `holdings` (each `instrumentId`, `quantity`, `value`),
  `totalValue` and `paidIn`.

All money amounts are JSON numbers in euros with two decimal places.

---

## Upvest tooling — non-negotiable

- Use the **upvest-v4** plugin (from the **apimatic** marketplace) for
  **every** Upvest interaction. It is your sole reference for how to talk to Upvest.
- **Do not** web-search or rely on general/external knowledge for Upvest API details.
- If that source does not expose a capability you need, **STOP and report the gap** — do not
  invent or work around it.

---

## Sandbox entities & test fixtures

- Nothing is pre-seeded at Upvest. Use made-up personal details for test investors — never a
  real person's.
- The Upvest sandbox is live at `UPVEST_BASE_URL`, and these credentials work against it. Its
  client is authorised only for user operations: a call that needs any other permission is
  refused with `401` ("This client is not authorised to use these scopes"). That is this
  environment, not your code, and not a gap to report. Build every flow in full, and verify
  against the sandbox as much as it allows.

---

## Credentials

- Credentials arrive as env vars: `UPVEST_CLIENT_ID`, `UPVEST_CLIENT_SECRET`,
  `UPVEST_SIGNING_KEY_ID`, `UPVEST_SIGNING_KEY_PATH`, `UPVEST_SIGNING_KEY_PASSPHRASE`,
  `UPVEST_BASE_URL`.
- **Bind settings from the `Upvest:` configuration section using exactly these keys**, and
  hard-code none of their values: `Upvest:ClientId`, `Upvest:ClientSecret`,
  `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`,
  `Upvest:BaseUrl`.
- `Upvest:BaseUrl` is the base address for **every** call to Upvest; use it verbatim instead of
  any default.
- The client secret and the signing key passphrase are secrets: never logged, never returned by
  an endpoint, never written into a source file.

---

## Environment gotchas (this machine)

- **SDK/runtime mismatch:** `global.json` pins the SDK to 8.0.x, but only the .NET 10 SDK is
  installed and the ASP.NET Core 8.0 runtime is missing. Let it roll forward
  (`rollForward: latestMajor`) and run with `DOTNET_ROLL_FORWARD=Major`, or install the
  ASP.NET Core 8.0 runtime (x64).
- **No SQL Server LocalDB:** default connection strings point at `(localdb)\mssqllocaldb`,
  which isn't here. Run with `UseOnlyInMemoryDatabase=true`. Caveat: the in-memory provider
  loses all data on restart and ignores migrations — so investors, models and payments only
  survive within a single run.
- **Per-host in-memory stores:** with the in-memory provider, Web and PublicApi each hold
  their **own isolated** store. Keep every flow verifiable end-to-end through PublicApi alone.
- **Two hosts, two auth models:** Web = cookie, `https://localhost:5001`; PublicApi = JWT on
  its own ports. For curl/Postman against PublicApi, get a bearer token from its authenticate
  endpoint first — the storefront cookie won't work there.
- **HTTPS dev cert:** both hosts use `UseHttpsRedirection()`; ensure the dev cert is trusted
  (`dotnet dev-certs https --check`).
- **Ports:** when you run services, bind only to your assigned block
  (`APP_PORT_BLOCK_BASE` … `+APP_PORT_BLOCK_SIZE-1`; `launchSettings` already points there).
  Stop your previous instance before starting another — no stray processes on stale builds.

There is otherwise no infra dependency beyond the .NET SDK/runtime — no Docker, no broker,
no PostgreSQL. Don't introduce any.

---

## Rules of engagement

- We want a **production-grade** integration — you decide what production-grade looks like.
- When done, **self-verify** that it builds, that the app starts, and that every endpoint above
  behaves as specified as far as the sandbox allows (see above). Then give me a concise,
  step-by-step guide to verify the working integration myself.

---

## Constraints

- **Secrets never enter the repository.** Read the API credentials from the environment
  variables above and load them into **.NET user-secrets** yourself. Never write their
  **values** into any file inside this repository — not into `appsettings*.json`, not into
  a launch profile, a script, a test fixture, a comment, or a commit message. Referencing
  the variable/secret **names** is fine, the values are not.
- **Report a gap only when it is genuinely a gap.** Stop and report when the source you were
  given does not cover a capability this integration requires. A design decision being hard,
  open-ended, or left to your judgment is **not** a gap — decide it and proceed.
- **You are running headless — there is no one to answer you.** Work until the integration
  is fully complete. Never hand back, never end with a question, and never defer remaining
  work to the user: decide and proceed.

