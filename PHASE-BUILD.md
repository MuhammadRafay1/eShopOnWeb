
# Task — Add "Invest your change" to eShopOnWeb

Let eShopOnWeb shoppers invest their spare change, with **Upvest** as the investment provider.
A shopper who opts in has every order they pay for rounded up to the next whole euro; the
difference is set aside, and once enough has been set aside it is invested on their behalf in a
single exchange-traded fund. The shopper can see what they have set aside and what has been
invested. eShopOnWeb today has no notion of an investor, of spare change, or of anything held on
the shopper's behalf outside the shop. This is an **additive** capability — it does not replace
the existing catalog/basket/order flow.

You own the design and every implementation decision — architecture, file layout, build order,
patterns. Just honor the mandates and the details below.

---

## What to build

### Flow 1 — Opting in

A logged-in shopper opts in to investing their change.

- `POST /api/investing/enrolment` — opt the signed-in shopper in. The request carries the
  shop's investor sign-up form: `firstName`, `lastName`, `email`, `birthDate` (ISO-8601 date),
  `nationality` (ISO 3166-1 alpha-2), `address` (`line1`, `postcode`, `city`, `country`),
  `phoneNumber`, `taxId` and `taxCountry`. The shopper becomes an investor with Upvest, able to
  hold what is bought for them.
- `GET /api/investing/enrolment` — where the caller's enrolment has got to.

A shopper can only invest once Upvest has accepted them as an investor.

A shopper's enrolment, ledger and investments belong to that shopper alone: one shopper must
never see another's. Personal details are never written to logs.

### Flow 2 — Setting aside the change

- `POST /api/orders` — place an order from catalog items; the request carries catalog item ids
  and quantities and reuses the app's existing order/order-item model rather than a parallel one
  (the caller's identity comes from the token).

eShopOnWeb has no separate payment step. For an enrolled shopper, each paid order sets aside the
difference between its total and the next whole euro: an order of €12.30 sets aside €0.70, and
an order whose total is already a whole number of euros sets aside nothing. Catalog prices are
euro amounts. Orders from a shopper who is not an accepted investor set nothing aside. Where in
the application an order becomes "paid" is your design decision.

Placing an order must never fail because of anything to do with investing — the order is
still placed and the caller's request still succeeds.

### Flow 3 — Investing it

Once a shopper's set-aside balance reaches **€10**, the whole balance is invested for them in
the fund named in configuration (`Upvest:InstrumentId`, below). Afterwards the balance starts
again from zero, and whatever is set aside from then on accrues towards the next investment.

- `GET /api/investing/investments` — the caller's investments, newest first, each showing how
  much was invested and where it has got to.

### Flow 4 — Settlement

Each investment's status in this application must reflect what actually happened to it at
Upvest.

### Flow 5 — The shopper's balance

- `GET /api/investing/balance` — what the caller currently has set aside and not yet invested,
  and the total amount invested so far.

### The Upvest connection

Every call this application makes to Upvest goes through **one reusable `DelegatingHandler`**
that takes care of authenticating the call. No call site attaches credentials itself.

### Where it goes

Expose all capabilities as HTTP endpoints on the **`src/PublicApi`** project (JWT-authenticated;
the caller's identity comes from the token), following that project's existing endpoint
conventions, routed under `/api/` as named above. Every flow above has to be drivable through
that API alone. Any route Upvest itself calls is yours to choose, and is the only kind of route that
does not take the shopper's token. No storefront UI is required.

### Response fields

So the flows can be driven end to end by a caller, these names are fixed; everything else about
the response shape is your call.

- `POST /api/orders` returns `orderId` and `roundUpAmount` (the amount that order set aside, `0`
  when it set aside nothing).
- `POST /api/investing/enrolment` and `GET /api/investing/enrolment` return `enrolmentId` and
  `status`, which is `pending` until Upvest has accepted the shopper, then `active` — or
  `rejected` if Upvest will not take them on.
- Each entry of `GET /api/investing/investments` carries `investmentId`, `amount` and `status`,
  which is `pending` until the investment's outcome at Upvest is known, then `settled` or
  `failed`.
- `GET /api/investing/balance` returns `pendingAmount` and `investedAmount`.

All money amounts are JSON numbers in euros with two decimal places.

---

## Upvest tooling — non-negotiable

- Use the **upvest-docs** MCP server for **every** Upvest question. It is your sole
  reference for how to talk to Upvest.
- **Do not** web-search or rely on general/external knowledge for Upvest API details.
- If that source does not expose a capability you need, **STOP and report the gap** — do not
  invent or work around it.

---

## Sandbox entities & test fixtures

- Nothing is pre-seeded at Upvest. Use made-up personal details for test investors — never a
  real person's.
- `UPVEST_CALLBACK_BASE_URL` is the public base address at which this application's PublicApi
  host can be reached from outside.

---

## Credentials

- Credentials arrive as env vars: `UPVEST_CLIENT_ID`, `UPVEST_CLIENT_SECRET`,
  `UPVEST_SIGNING_KEY_ID`, `UPVEST_SIGNING_KEY_PATH`,
  `UPVEST_SIGNING_KEY_PASSPHRASE`, `UPVEST_BASE_URL`, `UPVEST_INSTRUMENT_ID`,
  `UPVEST_CALLBACK_BASE_URL`.
- **Bind settings from the `Upvest:` configuration section using exactly these keys**, and
  hard-code none of their values: `Upvest:ClientId`, `Upvest:ClientSecret`,
  `Upvest:SigningKeyId`, `Upvest:SigningKeyPath`, `Upvest:SigningKeyPassphrase`,
  `Upvest:BaseUrl`, `Upvest:InstrumentId`, `Upvest:CallbackBaseUrl`.
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
  loses all data on restart and ignores migrations — so enrolments, ledgers and investments
  only survive within a single run.
- **Per-host in-memory stores:** with the in-memory provider, Web and PublicApi each hold
  their **own isolated** store — an order placed through the Web storefront is invisible to
  PublicApi. Keep every flow verifiable end-to-end through PublicApi alone (that is why
  `POST /api/orders` is part of the surface).
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
- When done, **self-verify** that it builds and the flows actually work — a shopper enrolled and
  accepted, enough orders placed to cross €10, the balance invested, and the investment's final
  status shown. Then give me a concise,
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

