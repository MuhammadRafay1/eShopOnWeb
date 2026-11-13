using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Orchestrates the Upvest flows (onboarding, models, pay-ins, realignment, portfolio) over
/// <see cref="UpvestApiClient"/>. A missing scope in the current sandbox surfaces as
/// <c>Determined = false</c> so the application keeps whatever status it last learned from Upvest.
/// </summary>
public sealed class UpvestService : IUpvestService
{
    private const string ScopeBlockedNote = "This Upvest sandbox client is only authorised for user operations, so this step could not be completed here.";

    private readonly UpvestApiClient _client;

    public UpvestService(UpvestApiClient client) => _client = client;

    // ---------------------------------------------------------------- Onboarding (Flow 1)

    public async Task<OnboardingResult> OnboardInvestorAsync(InvestorSignup s, CancellationToken ct = default)
    {
        // 1) Create the user. This is the one step authorised in the sandbox and must succeed.
        var userBody = BuildUserBody(s);
        string userId;
        string userStatus;
        using (var res = await _client.PostAsync("/users", UpvestScopes.UsersAdmin, userBody, ct))
        {
            if (!res.IsSuccess)
            {
                throw new UpvestException($"Upvest rejected the investor sign-up (status {res.StatusCode}): {res.ProblemDetail ?? "no detail"}.");
            }
            userId = res.Body.GetProperty("id").GetString()!;
            userStatus = res.Body.TryGetProperty("status", out var st) ? st.GetString() ?? "INACTIVE" : "INACTIVE";
        }

        string? accountGroupId = null;
        string? accountId = null;
        string? accountStatus = null;
        var notes = new List<string>();

        // 2) Submit the KYC check (identity document the shop verified).
        if (!await TryStepAsync(() => _client.PostAsync($"/users/{userId}/checks", UpvestScopes.ChecksAdmin, BuildKycCheckBody(s), ct), "KYC check", notes, ct))
        {
            // Without checks the user cannot activate; still return the created user.
        }

        // 3) Declare tax residency.
        await TryStepAsync(() => _client.PostAsync($"/users/{userId}/tax_residencies", UpvestScopes.TaxesAdmin, BuildTaxBody(s), ct), "tax residency", notes, ct);

        // 4) Create the account group.
        accountGroupId = await TryCreateAsync(() => _client.PostAsync("/account_groups", UpvestScopes.AccountsAdmin,
            new { user_id = userId, type = "PERSONAL" }, ct), "account group", notes, ct);

        // 5) Create the (PORTFOLIO) account within the group.
        if (accountGroupId is not null)
        {
            using var res = await SafeAsync(() => _client.PostAsync("/accounts", UpvestScopes.AccountsAdmin,
                new { user_id = userId, account_group_id = accountGroupId, type = "PORTFOLIO" }, ct), "account", notes);
            if (res is { IsSuccess: true })
            {
                accountId = res.Body.GetProperty("id").GetString();
                accountStatus = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            }
        }

        var status = MapInvestorStatus(userStatus, accountStatus, accountReady: accountId is not null);
        var detail = BuildOnboardingDetail(userStatus, accountId, notes);
        return new OnboardingResult(userId, accountGroupId, accountId, status, detail);
    }

    public async Task<InvestorStatusResult> GetInvestorStatusAsync(string upvestUserId, string? upvestAccountId, CancellationToken ct = default)
    {
        string? userStatus = null;
        string? accountStatus = null;
        var determined = false;

        try
        {
            using var res = await _client.GetAsync($"/users/{upvestUserId}", UpvestScopes.UsersRead, ct);
            if (res.IsSuccess && res.HasBody)
            {
                determined = true;
                userStatus = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            }
        }
        catch (UpvestScopeNotAuthorizedException) { /* keep last known */ }

        if (upvestAccountId is not null)
        {
            try
            {
                using var res = await _client.GetAsync($"/accounts/{upvestAccountId}", UpvestScopes.AccountsRead, ct);
                if (res.IsSuccess && res.HasBody)
                {
                    determined = true;
                    accountStatus = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
                }
            }
            catch (UpvestScopeNotAuthorizedException) { /* keep last known */ }
        }

        if (!determined)
        {
            return new InvestorStatusResult(false, InvestorStatus.Pending, ScopeBlockedNote);
        }

        var mapped = MapInvestorStatus(userStatus, accountStatus, accountReady: upvestAccountId is not null && accountStatus is not null);
        return new InvestorStatusResult(true, mapped, null);
    }

    // ---------------------------------------------------------------- Models (Flow 2)

    public async Task<AllocationResult> SaveAllocationAsync(string? existingAllocationId, string name, IReadOnlyList<FundWeight> funds, CancellationToken ct = default)
    {
        var allocation = funds.Select(f => new
        {
            instrument_id = f.InstrumentId,
            instrument_id_type = "ISIN",
            weight = (f.WeightPercent / 100m).ToString("0.######", CultureInfo.InvariantCulture)
        }).ToArray();
        var body = new { name, allocation };

        try
        {
            using var res = existingAllocationId is null
                ? await _client.PostAsync("/portfolios/allocations", UpvestScopes.PortfoliosAdmin, body, ct)
                : await _client.PutAsync($"/portfolios/allocations/{existingAllocationId}", UpvestScopes.PortfoliosAdmin, body, ct);

            if (!res.IsSuccess)
            {
                return new AllocationResult(false, existingAllocationId, $"Upvest allocation save returned {res.StatusCode}: {res.ProblemDetail ?? "no detail"}.");
            }
            var id = res.Body.TryGetProperty("id", out var i) ? i.GetString() : existingAllocationId;
            return new AllocationResult(true, id, null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new AllocationResult(false, existingAllocationId, ScopeBlockedNote);
        }
    }

    public async Task<DeterminedResult> ConfigurePortfolioAsync(string accountId, string allocationId, bool alreadyConfigured, CancellationToken ct = default)
    {
        try
        {
            using var res = alreadyConfigured
                ? await _client.PatchAsync($"/portfolios/configurations/{accountId}", UpvestScopes.PortfoliosAdmin, new { allocation_id = allocationId }, ct)
                : await _client.PostAsync("/portfolios/configurations", UpvestScopes.PortfoliosAdmin, new { account_id = accountId, allocation_id = allocationId }, ct);
            return res.IsSuccess
                ? new DeterminedResult(true, null)
                : new DeterminedResult(false, $"Upvest configuration returned {res.StatusCode}: {res.ProblemDetail ?? "no detail"}.");
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new DeterminedResult(false, ScopeBlockedNote);
        }
    }

    // ---------------------------------------------------------------- Pay-in (Flow 3)

    public async Task<PayInResult> PayInAsync(string accountGroupId, string upvestUserId, string accountId, decimal amount, string currency, CancellationToken ct = default)
    {
        var cash = amount.ToString("0.00", CultureInfo.InvariantCulture);

        // Credit the account group cash balance (sandbox funding via top-up).
        string? topupId = null;
        try
        {
            using var topup = await _client.PostAsync("/payments/topups", UpvestScopes.TopupsAdmin,
                new { account_group_id = accountGroupId, cash_amount = cash, currency }, ct);
            if (topup.IsSuccess)
            {
                topupId = topup.Body.TryGetProperty("id", out var i) ? i.GetString() : null;
            }
            else
            {
                return new PayInResult(false, null, null, PaymentStatus.Pending, $"Upvest top-up returned {topup.StatusCode}: {topup.ProblemDetail ?? "no detail"}.");
            }
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new PayInResult(false, null, null, PaymentStatus.Pending, ScopeBlockedNote);
        }

        // Invest the credited cash across the model's funds.
        try
        {
            using var order = await _client.PostAsync("/portfolios/orders", UpvestScopes.PortfoliosAdmin,
                new { user_id = upvestUserId, account_id = accountId, side = "BUY", cash_amount = cash, currency }, ct);
            if (!order.IsSuccess)
            {
                return new PayInResult(false, topupId, null, PaymentStatus.Pending, $"Upvest portfolio order returned {order.StatusCode}: {order.ProblemDetail ?? "no detail"}.");
            }
            var orderId = order.Body.TryGetProperty("id", out var i) ? i.GetString() : null;
            var orderStatus = order.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            return new PayInResult(true, topupId, orderId, MapPaymentStatus(orderStatus), null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new PayInResult(false, topupId, null, PaymentStatus.Pending, ScopeBlockedNote);
        }
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string portfolioOrderId, CancellationToken ct = default)
    {
        try
        {
            using var res = await _client.GetAsync($"/portfolios/orders/{portfolioOrderId}", UpvestScopes.PortfoliosRead, ct);
            if (!res.IsSuccess || !res.HasBody)
            {
                return new PaymentStatusResult(false, PaymentStatus.Pending, res.ProblemDetail);
            }
            var orderStatus = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            return new PaymentStatusResult(true, MapPaymentStatus(orderStatus), null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new PaymentStatusResult(false, PaymentStatus.Pending, ScopeBlockedNote);
        }
    }

    // ---------------------------------------------------------------- Realignment (Flow 4)

    public async Task<RealignmentTriggerResult> TriggerRealignmentAsync(string allocationId, CancellationToken ct = default)
    {
        try
        {
            using var res = await _client.PostAsync("/portfolios/rebalancings/executions", UpvestScopes.PortfoliosAdmin,
                new { allocations = new[] { allocationId } }, ct);
            if (!res.IsSuccess)
            {
                return new RealignmentTriggerResult(false, null, RealignmentStatus.Pending, $"Upvest realignment returned {res.StatusCode}: {res.ProblemDetail ?? "no detail"}.");
            }
            var id = res.Body.TryGetProperty("id", out var i) ? i.GetString() : null;
            var status = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            return new RealignmentTriggerResult(true, id, MapRealignmentStatus(status), null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new RealignmentTriggerResult(false, null, RealignmentStatus.Pending, ScopeBlockedNote);
        }
    }

    public async Task<RealignmentStatusResult> GetRealignmentStatusAsync(string executionId, CancellationToken ct = default)
    {
        try
        {
            using var res = await _client.GetAsync($"/portfolios/rebalancings/executions/{executionId}", UpvestScopes.PortfoliosRead, ct);
            if (!res.IsSuccess || !res.HasBody)
            {
                return new RealignmentStatusResult(false, RealignmentStatus.Pending, res.ProblemDetail);
            }
            var status = res.Body.TryGetProperty("status", out var st) ? st.GetString() : null;
            return new RealignmentStatusResult(true, MapRealignmentStatus(status), null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new RealignmentStatusResult(false, RealignmentStatus.Pending, ScopeBlockedNote);
        }
    }

    // ---------------------------------------------------------------- Portfolio (Flow 5)

    public async Task<PortfolioResult> GetPortfolioAsync(string accountId, CancellationToken ct = default)
    {
        try
        {
            using var res = await _client.GetAsync($"/accounts/{accountId}/valuations/current", UpvestScopes.ValuationsRead, ct);
            if (!res.IsSuccess || !res.HasBody)
            {
                return new PortfolioResult(false, Array.Empty<PortfolioHolding>(), 0m, res.ProblemDetail ?? ScopeBlockedNote);
            }

            var holdings = new List<PortfolioHolding>();
            if (res.Body.TryGetProperty("security_positions", out var positions) && positions.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in positions.EnumerateArray())
                {
                    var isin = p.TryGetProperty("instrument", out var ins) && ins.TryGetProperty("isin", out var i) ? i.GetString() ?? "" : "";
                    var qty = ParseDecimal(p, "quantity");
                    var value = p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object ? ParseDecimal(v, "amount") : 0m;
                    holdings.Add(new PortfolioHolding(isin, qty, value));
                }
            }

            var total = res.Body.TryGetProperty("total_security_value", out var tv) && tv.ValueKind == JsonValueKind.Object
                ? ParseDecimal(tv, "amount")
                : holdings.Sum(h => h.Value);

            return new PortfolioResult(true, holdings, total, null);
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            return new PortfolioResult(false, Array.Empty<PortfolioHolding>(), 0m, ScopeBlockedNote);
        }
    }

    // ---------------------------------------------------------------- Request body builders

    private static object BuildUserBody(InvestorSignup s)
    {
        var digits = new string(s.PhoneNumber.Where(char.IsDigit).ToArray());
        object body = new
        {
            first_name = s.FirstName,
            last_name = s.LastName,
            email = s.Email,
            birth_date = s.BirthDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            birth_city = string.IsNullOrWhiteSpace(s.BirthCity) ? null : s.BirthCity,
            birth_country = string.IsNullOrWhiteSpace(s.BirthCountry) ? null : s.BirthCountry.ToUpperInvariant(),
            nationalities = new[] { s.Nationality.ToUpperInvariant() },
            address = new
            {
                address_line1 = s.Address.Line1,
                postcode = s.Address.Postcode,
                city = s.Address.City,
                country = s.Address.Country.ToUpperInvariant()
            },
            phone_number = digits.Length is >= 8 and <= 15 ? digits : null,
            // Upvest requires an explicit FATCA declaration; the shop collected consent at sign-up.
            fatca = new { status = false, confirmed_at = UtcNow() }
        };
        return body;
    }

    private static object BuildKycCheckBody(InvestorSignup s) => new
    {
        type = "KYC",
        check_confirmed_at = UtcNow(),
        document_type = MapDocumentType(s.IdentityDocument.Type),
        document_expiration_date = s.IdentityDocument.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        nationality = s.Nationality.ToUpperInvariant(),
        provider = "eShopOnWeb",
        method = "VIDEO_ID",
        // Supplying the confirmed address lets Upvest satisfy proof-of-residence automatically.
        confirmed_address = new
        {
            address_line1 = s.Address.Line1,
            postcode = s.Address.Postcode,
            city = s.Address.City,
            country = s.Address.Country.ToUpperInvariant()
        }
    };

    private static object BuildTaxBody(InvestorSignup s) => new
    {
        tax_residencies = new[]
        {
            new { country = s.TaxCountry.ToUpperInvariant(), tax_identifier_number = s.TaxId }
        }
    };

    // ---------------------------------------------------------------- Helpers

    private static string UtcNow() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string MapDocumentType(string type)
    {
        var t = (type ?? "").Trim().ToUpperInvariant().Replace(' ', '_');
        return t switch
        {
            "PASSPORT" => "PASSPORT",
            "ID_CARD" or "NATIONAL_ID" or "IDENTITY_CARD" => "ID_CARD",
            "DRIVERS_LICENSE" or "DRIVING_LICENCE" or "DRIVING_LICENSE" => "DRIVERS_LICENSE",
            "" => "ID_CARD",
            _ => t
        };
    }

    private static decimal ParseDecimal(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var el)) return 0m;
        return el.ValueKind switch
        {
            JsonValueKind.String => decimal.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
            JsonValueKind.Number => el.GetDecimal(),
            _ => 0m
        };
    }

    private static InvestorStatus MapInvestorStatus(string? userStatus, string? accountStatus, bool accountReady)
    {
        var u = userStatus?.ToUpperInvariant();
        var a = accountStatus?.ToUpperInvariant();
        if (u is "REJECTED" or "BLOCKED" or "DECEASED" || a is "REJECTED" or "BLOCKED" or "CLOSED")
        {
            return InvestorStatus.Rejected;
        }
        if (u == "ACTIVE" && accountReady && a == "ACTIVE")
        {
            return InvestorStatus.Active;
        }
        return InvestorStatus.Pending;
    }

    private static PaymentStatus MapPaymentStatus(string? orderStatus) => orderStatus?.ToUpperInvariant() switch
    {
        "FILLED" or "COMPLETED" or "SETTLED" => PaymentStatus.Invested,
        "CANCELLED" or "PARTIALLY_CANCELLED" or "REJECTED" or "FAILED" or "EXPIRED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static RealignmentStatus MapRealignmentStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "COMPLETED" or "FILLED" or "SETTLED" or "DONE" => RealignmentStatus.Completed,
        "FAILED" or "CANCELLED" or "REJECTED" => RealignmentStatus.Failed,
        _ => RealignmentStatus.Pending
    };

    private static string BuildOnboardingDetail(string userStatus, string? accountId, List<string> notes)
    {
        if (accountId is not null && userStatus.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return "Investor accepted by Upvest and account ready.";
        }
        var parts = new List<string> { "Investor created with Upvest; awaiting acceptance and account readiness." };
        if (notes.Count > 0)
        {
            parts.Add(string.Join(" ", notes));
        }
        return string.Join(" ", parts);
    }

    /// <summary>Runs a step, swallowing scope-block and non-success, recording a note. Returns true on success.</summary>
    private static async Task<bool> TryStepAsync(Func<Task<UpvestResult>> call, string stepName, List<string> notes, CancellationToken ct)
    {
        using var res = await SafeAsync(call, stepName, notes);
        return res is { IsSuccess: true };
    }

    private static async Task<string?> TryCreateAsync(Func<Task<UpvestResult>> call, string stepName, List<string> notes, CancellationToken ct)
    {
        using var res = await SafeAsync(call, stepName, notes);
        if (res is { IsSuccess: true } && res.Body.TryGetProperty("id", out var id))
        {
            return id.GetString();
        }
        return null;
    }

    private static async Task<UpvestResult?> SafeAsync(Func<Task<UpvestResult>> call, string stepName, List<string> notes)
    {
        try
        {
            var res = await call();
            if (!res.IsSuccess)
            {
                notes.Add($"The {stepName} step could not be completed (Upvest returned {res.StatusCode}).");
            }
            return res;
        }
        catch (UpvestScopeNotAuthorizedException)
        {
            notes.Add($"The {stepName} step was skipped: {ScopeBlockedNote}");
            return null;
        }
    }
}
