using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpvestInvestmentApi;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using ReqUsers = UpvestInvestmentApi.Requests.Users;
using ReqChecks = UpvestInvestmentApi.Requests.UserChecks;
using ReqTax = UpvestInvestmentApi.Requests.TaxResidencies;
using ReqGroups = UpvestInvestmentApi.Requests.AccountGroups;
using ReqAccounts = UpvestInvestmentApi.Requests.AccountsApi;
using Enums = UpvestInvestmentApi.Models.Enums;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio.Services;

/// <summary>
/// Flow 1 — onboarding a shopper as a Upvest investor with a PORTFOLIO account. The chain is idempotent and
/// resumable: one investor row per shopper (keyed by the JWT subject) is claimed first, persisted idempotency
/// keys let any create be safely replayed, and each step runs only if its result is still missing.
/// </summary>
public sealed class InvestorService : IInvestorService
{
    private readonly UpvestInvestmentApiClient _client;
    private readonly PortfolioContext _db;
    private readonly UpvestSettings _settings;
    private readonly ILogger<InvestorService> _logger;

    public InvestorService(UpvestInvestmentApiClient client, PortfolioContext db,
        IOptions<UpvestSettings> settings, ILogger<InvestorService> logger)
    {
        _client = client;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<InvestorStatusResult> SignUpAsync(string shopperId, InvestorSignupDetails details, CancellationToken ct)
    {
        var investor = await ClaimInvestorAsync(shopperId, ct);

        // 1. Create the Upvest user (TOL model).
        if (investor.UpvestUserId is null)
        {
            var created = await UpvestCall.WriteAsync(() => _client.Users.CreateUser(
                new ReqUsers.CreateUserRequest
                {
                    IdempotencyKey = investor.CreateUserIdempotencyKey,
                    Body = UserCreateRequest.UserTolCreateRequest(BuildUser(details))
                }, cancellationToken: ct), "CreateUser", _logger);

            if (!created.TryGetUserTol(out var user))
                throw new PortfolioProviderException("The investment provider returned an unexpected sign-up result.", null);

            investor.UpvestUserId = user.Id;
            await _db.SaveChangesAsync(ct);
        }

        var userId = investor.UpvestUserId!.Value;

        // 2. KYC check — carries the identity document the shop verified.
        if (investor.KycCheckId is null)
        {
            var kyc = await UpvestCall.WriteAsync(() => _client.UserChecks.CreateUserCheck(
                new ReqChecks.CreateUserCheckRequest
                {
                    UserId = userId,
                    Body = UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(BuildKyc(details))
                }, cancellationToken: ct), "CreateUserCheck(KYC)", _logger);
            investor.KycCheckId = kyc.Id;
            await _db.SaveChangesAsync(ct);
        }

        // 3. Instrument-fit (suitability) check.
        if (investor.InstrumentFitCheckId is null)
        {
            var fit = await UpvestCall.WriteAsync(() => _client.UserChecks.CreateUserCheck(
                new ReqChecks.CreateUserCheckRequest
                {
                    UserId = userId,
                    Body = UserCheckCreateRequest.UserCheckInstrumentFitCreateRequest(new UserCheckInstrumentFitCreateRequest
                    {
                        CheckConfirmedAt = DateTimeOffset.UtcNow,
                        InstrumentSuitability = new InstrumentSuitability { Suitability = true }
                    })
                }, cancellationToken: ct), "CreateUserCheck(InstrumentFit)", _logger);
            investor.InstrumentFitCheckId = fit.Id;
            await _db.SaveChangesAsync(ct);
        }

        // 4. Tax residency (full-replace: always the complete set).
        await UpvestCall.WriteAsync(() => _client.TaxResidencies.SetTaxResidencies(
            new ReqTax.SetTaxResidenciesRequest
            {
                UserId = userId,
                IdempotencyKey = investor.CreateUserIdempotencyKey,
                Body = new TaxResidenciesSetRequest
                {
                    TaxResidencies = new[]
                    {
                        TaxResidencyForCreateRequest.WithTaxIdentifierNumber(new WithTaxIdentifierNumber
                        {
                            Country = UpvestMapping.Country(details.TaxCountry, "taxCountry"),
                            TaxIdentifierNumber = details.TaxId
                        })
                    }
                }
            }, cancellationToken: ct), "SetTaxResidencies", _logger);

        // 5. Account group (PERSONAL).
        if (investor.AccountGroupId is null)
        {
            var group = await UpvestCall.WriteAsync(() => _client.AccountGroups.CreateAccountGroup(
                new ReqGroups.CreateAccountGroupRequest
                {
                    IdempotencyKey = investor.AccountGroupIdempotencyKey,
                    Body = AccountGroupCreateRequest.AccountGroupCreateUserRequest(new AccountGroupCreateUserRequest
                    {
                        UserId = userId,
                        Type = Enums.Type13.Personal
                    })
                }, cancellationToken: ct), "CreateAccountGroup", _logger);

            if (!group.TryGetAccountGroup(out var ag))
                throw new PortfolioProviderException("The investment provider returned an unexpected account-group result.", null);
            investor.AccountGroupId = ag.Id;
            await _db.SaveChangesAsync(ct);
        }

        // 6. PORTFOLIO account — able to hold a managed portfolio.
        if (investor.AccountId is null)
        {
            var account = await UpvestCall.WriteAsync(() => _client.AccountsApi.CreateAccount(
                new ReqAccounts.CreateAccountRequest
                {
                    IdempotencyKey = investor.AccountIdempotencyKey,
                    Body = AccountCreateRequest.AccountCreateUserRequest(new AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = investor.AccountGroupId!.Value,
                        Type = Enums.Type16.Portfolio
                    })
                }, cancellationToken: ct), "CreateAccount", _logger);

            if (!account.TryGetAccount(out var acc))
                throw new PortfolioProviderException("The investment provider returned an unexpected account result.", null);
            investor.AccountId = acc.Id;
            await _db.SaveChangesAsync(ct);
        }

        var status = await DeriveStatusAsync(investor, ct);
        investor.Status = status;
        await _db.SaveChangesAsync(ct);
        return new InvestorStatusResult(investor.UpvestUserId, status);
    }

    public async Task<InvestorStatusResult?> GetStatusAsync(string shopperId, CancellationToken ct)
    {
        var investor = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (investor is null) return null;

        try
        {
            var status = await DeriveStatusAsync(investor, ct);
            if (status != investor.Status)
            {
                investor.Status = status;
                await _db.SaveChangesAsync(ct);
            }
            return new InvestorStatusResult(investor.UpvestUserId, status);
        }
        catch (PortfolioProviderException)
        {
            // Re-read not permitted/available right now — report the last-known status rather than failing.
            return new InvestorStatusResult(investor.UpvestUserId, investor.Status);
        }
    }

    /// <summary>Reads the current status from Upvest. ACTIVE→active; a failed KYC→rejected; otherwise pending.</summary>
    private async Task<InvestorSignupStatus> DeriveStatusAsync(Investor investor, CancellationToken ct)
    {
        if (investor.UpvestUserId is null) return InvestorSignupStatus.Pending;

        var response = await UpvestCall.ReadAsync(() => _client.Users.RetrieveUser(
            new ReqUsers.RetrieveUserRequest { UserId = investor.UpvestUserId.Value }, cancellationToken: ct),
            "RetrieveUser", _logger);

        if (response.TryGetUserTol(out var user) && user.Status == Enums.Status.Active)
            return InvestorSignupStatus.Active;

        // Not active: a failed identity check means Upvest will not take the shopper on.
        if (investor.KycCheckId is not null && await IsKycFailedAsync(investor.UpvestUserId.Value, investor.KycCheckId.Value, ct))
            return InvestorSignupStatus.Rejected;

        return InvestorSignupStatus.Pending;
    }

    private async Task<bool> IsKycFailedAsync(Guid userId, Guid checkId, CancellationToken ct)
    {
        try
        {
            var check = await UpvestCall.ReadAsync(() => _client.UserChecks.RetrieveUserCheck(
                new ReqChecks.RetrieveUserCheckRequest { UserId = userId, CheckId = checkId }, cancellationToken: ct),
                "RetrieveUserCheck", _logger);
            return check.TryGetUserCheckKnowYourCustomer(out var kyc) && kyc.Status == Enums.Status6.Failed;
        }
        catch (PortfolioProviderException)
        {
            // Can't read the check (scope/availability) — do not claim a rejection we cannot confirm.
            return false;
        }
    }

    private async Task<Investor> ClaimInvestorAsync(string shopperId, CancellationToken ct)
    {
        var existing = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (existing is not null) return existing;

        var investor = new Investor
        {
            ShopperId = shopperId,
            Status = InvestorSignupStatus.Pending,
            CreateUserIdempotencyKey = Guid.NewGuid(),
            AccountGroupIdempotencyKey = Guid.NewGuid(),
            AccountIdempotencyKey = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Investors.Add(investor);
        try
        {
            await _db.SaveChangesAsync(ct);
            return investor;
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is ArgumentException || ex is InvalidOperationException)
        {
            // A concurrent claim won the primary key — reload and use the winner's row.
            _db.Entry(investor).State = EntityState.Detached;
            var winner = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
            return winner ?? throw new PortfolioProviderException("Could not record the investor sign-up.", null, ex);
        }
    }

    private UserTolCreateRequest BuildUser(InvestorSignupDetails d)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserTolCreateRequest
        {
            FirstName = d.FirstName,
            LastName = d.LastName,
            Email = d.Email,
            BirthDate = UpvestMapping.ToDateTimeOffset(d.BirthDate),
            BirthCity = d.BirthCity,
            BirthCountry = UpvestMapping.BirthCountry(d.BirthCountry, "birthCountry"),
            Nationalities = new[] { UpvestMapping.Nationality(d.Nationality, "nationality") },
            PhoneNumber = UpvestMapping.PhoneDigits(d.PhoneNumber),
            Address = new Address2
            {
                AddressLine1 = d.Address.Line1,
                Postcode = d.Address.Postcode,
                City = d.Address.City,
                Country = UpvestMapping.Country(d.Address.Country, "address.country")
            },
            // The shop verified the shopper; FATCA (US tax) status is declared not-a-US-person at verification time.
            Fatca = new Fatca { Status = false, ConfirmedAt = now },
            // Consent captured by the shop at sign-up (backend-required even though the schema marks them optional).
            TermsAndConditions = new TermsAndConditions { ConsentDocumentId = _settings.TermsConsentDocumentId, ConfirmedAt = now },
            DataPrivacyAndSharingAgreement = new DataPrivacyAndSharingAgreement { ConsentDocumentId = _settings.DataPrivacyConsentDocumentId, ConfirmedAt = now }
        };
    }

    private static UserCheckKnowYourCustomerCreateRequest BuildKyc(InvestorSignupDetails d) => new()
    {
        CheckConfirmedAt = DateTimeOffset.UtcNow,
        // A reference to where the shop keeps the KYC evidence it collected (not personal data).
        DataDownloadLink = "https://eshoponweb.example.com/kyc-evidence",
        DocumentType = UpvestMapping.DocumentType(d.IdentityDocument.Type),
        DocumentExpirationDate = UpvestMapping.ToDateTimeOffset(d.IdentityDocument.ExpiryDate),
        Nationality = d.Nationality.Trim().ToUpperInvariant(),
        Provider = "eShopOnWeb",
        Method = Enums.Method.ElectronicId
    };
}
