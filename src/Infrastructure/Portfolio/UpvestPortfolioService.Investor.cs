using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.AccountGroups;
using UpvestInvestmentApi.Requests.AccountsApi;
using UpvestInvestmentApi.Requests.TaxResidencies;
using UpvestInvestmentApi.Requests.UserChecks;
using UpvestInvestmentApi.Requests.Users;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public partial class UpvestPortfolioService
{
    // Flow 1 — becoming an investor -----------------------------------------------------------------

    public async Task<InvestorStatusResult> SignUpInvestorAsync(
        string shopper, InvestorSignupForm form, CancellationToken cancellationToken)
    {
        // Resolve and validate caller-supplied ISO codes / document type up front (HTTP 400 on bad input).
        var nationality = ResolveNationality(form.Nationality);
        var addressCountry = ResolveCountry(form.Address.Country, "address.country");
        var birthCountry = ResolveBirthCountry(form.BirthCountry);
        var taxCountry = ResolveCountry(form.TaxCountry, "taxCountry");
        var documentType = ResolveDocumentType(form.IdentityDocument.Type);

        var now = DateTimeOffset.UtcNow;

        // Claim first: a second sign-up for the same shopper is rejected by the primary key before any
        // Upvest call is made. The claim is completed below with the Upvest user id.
        var record = new InvestorRecord
        {
            Shopper = shopper,
            Status = InvestorSignupStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        if (!await _repository.TryClaimInvestorAsync(record, cancellationToken))
        {
            throw new DuplicateException("This shopper is already registered as an investor.");
        }

        // Create the Upvest user (TOL). Deterministic idempotency key => a resend reaches Upvest at most once.
        var userBody = BuildUserCreateRequest(form, nationality, addressCountry, birthCountry);
        var createResponse = await CallAsync("CreateUser", c => _client.Users.CreateUser(
            new CreateUserRequest { IdempotencyKey = DeterministicKey($"create-user:{shopper}"), Body = userBody },
            cancellationToken: c), cancellationToken);

        record.UpvestUserId = ReadUserId(createResponse);
        await _repository.UpdateInvestorAsync(record, cancellationToken);

        // Remaining onboarding (KYC check, tax residency, account group, portfolio account). These require
        // scopes the sandbox client does not have, so a 401/403 here is the documented environment limit,
        // not a caller error: log it and leave the sign-up PENDING. Any other provider error is rethrown.
        await RunOnboardingAsync(record, form, documentType, addressCountry, taxCountry, cancellationToken);

        await RefreshInvestorStatusAsync(record, cancellationToken);

        return new InvestorStatusResult(record.UpvestUserId, record.Status);
    }

    public async Task<InvestorStatusResult?> GetInvestorStatusAsync(string shopper, CancellationToken cancellationToken)
    {
        var record = await _repository.GetInvestorAsync(shopper, cancellationToken);
        if (record is null)
        {
            return null;
        }

        await RefreshInvestorStatusAsync(record, cancellationToken);
        return new InvestorStatusResult(record.UpvestUserId, record.Status);
    }

    // ---- onboarding steps (best effort against sandbox scopes) ----

    private async Task RunOnboardingAsync(
        InvestorRecord record, InvestorSignupForm form, DocumentType3 documentType,
        Country addressCountry, Country taxCountry, CancellationToken cancellationToken)
    {
        var userId = record.UpvestUserId;

        // KYC identity check (carries the verified identity document the shop checked).
        try
        {
            var kyc = BuildKycCheck(form, documentType, addressCountry);
            await CallAsync("CreateUserCheck", c => _client.UserChecks.CreateUserCheck(
                new CreateUserCheckRequest { UserId = userId, Body = kyc }, cancellationToken: c), cancellationToken);
        }
        catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning("Skipping KYC check for the new investor: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
            return; // nothing further will succeed without scopes; stay PENDING.
        }

        // Tax residency.
        try
        {
            var taxBody = new TaxResidenciesSetRequest
            {
                TaxResidencies = new TaxResidencyForCreateRequest[]
                {
                    new WithTaxIdentifierNumber { Country = taxCountry, TaxIdentifierNumber = form.TaxId }
                }
            };
            await CallAsync("SetTaxResidencies", c => _client.TaxResidencies.SetTaxResidencies(
                new SetTaxResidenciesRequest
                {
                    UserId = userId,
                    IdempotencyKey = DeterministicKey($"tax:{record.Shopper}"),
                    Body = taxBody
                }, cancellationToken: c), cancellationToken);
        }
        catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning("Skipping tax residency for the new investor: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
            return;
        }

        // Account group.
        try
        {
            var groupType = Type13.TryGetKnownValue(_settings.Onboarding.AccountGroupType, out var gt) ? gt : Type13.Personal;
            var groupResponse = await CallAsync("CreateAccountGroup", c => _client.AccountGroups.CreateAccountGroup(
                new CreateAccountGroupRequest
                {
                    IdempotencyKey = DeterministicKey($"account-group:{record.Shopper}"),
                    Body = new AccountGroupCreateUserRequest { UserId = userId, Type = groupType }
                }, cancellationToken: c), cancellationToken);

            record.UpvestAccountGroupId = groupResponse.Match(
                onAccountGroup: g => g.Id,
                onBusinessAccountGroup: b => (Guid?)null,
                otherwise: _ => (Guid?)null);
            await _repository.UpdateInvestorAsync(record, cancellationToken);
        }
        catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning("Skipping account-group creation for the new investor: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
            return;
        }

        if (record.UpvestAccountGroupId is null)
        {
            return;
        }

        // Portfolio account.
        try
        {
            var accountResponse = await CallAsync("CreateAccount", c => _client.AccountsApi.CreateAccount(
                new CreateAccountRequest
                {
                    IdempotencyKey = DeterministicKey($"account:{record.Shopper}"),
                    Body = new AccountCreateUserRequest
                    {
                        UserId = userId,
                        AccountGroupId = record.UpvestAccountGroupId.Value,
                        Type = Type16.Portfolio
                    }
                }, cancellationToken: c), cancellationToken);

            record.UpvestAccountId = accountResponse.Match(
                onAccount: a => a.Id,
                onBusinessAccount: b => (Guid?)null,
                otherwise: _ => (Guid?)null);
            await _repository.UpdateInvestorAsync(record, cancellationToken);
        }
        catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning("Skipping portfolio-account creation for the new investor: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
        }
    }

    /// <summary>Re-read the sign-up state at Upvest and refresh the stored status. The account group's status
    /// is the authority for readiness; falls back to the stored status when a scope-limited read is refused.</summary>
    private async Task RefreshInvestorStatusAsync(InvestorRecord record, CancellationToken cancellationToken)
    {
        var newStatus = record.Status;

        if (record.UpvestAccountGroupId is Guid groupId)
        {
            try
            {
                var response = await CallAsync("RetrieveAccountGroup", c => _client.AccountGroups.RetrieveAccountGroup(
                    new RetrieveAccountGroupRequest { AccountGroupId = groupId }, cancellationToken: c), cancellationToken);

                var groupStatus = response.Match(
                    onAccountGroup: g => (Status18?)g.Status,
                    onBusinessAccountGroup: _ => (Status18?)null,
                    otherwise: _ => (Status18?)null);

                newStatus = MapAccountGroupStatus(groupStatus);
            }
            catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
            {
                _logger.LogWarning("Cannot refresh investor status from the account group: the Upvest client lacks the required scope ({Status}).", ex.ProviderStatusCode);
            }
        }
        else if (record.UpvestUserId != Guid.Empty)
        {
            // No account group yet — confirm the user exists at Upvest; remain PENDING until the group is active.
            var response = await CallAsync("RetrieveUser", c => _client.Users.RetrieveUser(
                new RetrieveUserRequest { UserId = record.UpvestUserId }, cancellationToken: c), cancellationToken);

            var userStatus = response.Match(
                onUserByol: u => u.Status,
                onUserTol: u => u.Status,
                otherwise: _ => (Status?)null);

            newStatus = (userStatus is not null && (userStatus == Status.Offboarding || userStatus == Status.Offboarded))
                ? InvestorSignupStatus.Rejected
                : InvestorSignupStatus.Pending;
        }

        if (newStatus != record.Status)
        {
            record.Status = newStatus;
            await _repository.UpdateInvestorAsync(record, cancellationToken);
        }
    }

    private static InvestorSignupStatus MapAccountGroupStatus(Status18? status)
    {
        if (status is null)
        {
            return InvestorSignupStatus.Pending;
        }
        if (status == Status18.Active)
        {
            return InvestorSignupStatus.Active;
        }
        if (status == Status18.Closing || status == Status18.Closed || status == Status18.Locked)
        {
            return InvestorSignupStatus.Rejected;
        }
        return InvestorSignupStatus.Pending; // PENDING_APPROVAL or an unknown value
    }

    // ---- request builders ----

    private UserCreateRequest BuildUserCreateRequest(
        InvestorSignupForm form, Nationality nationality, Country addressCountry, BirthCountry birthCountry)
    {
        var tol = new UserTolCreateRequest
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Email = form.Email,
            BirthDate = ToDateTimeOffset(form.BirthDate),
            BirthCity = form.BirthCity,
            BirthCountry = birthCountry,
            Nationalities = new[] { nationality },
            PhoneNumber = string.IsNullOrWhiteSpace(form.PhoneNumber) ? null : form.PhoneNumber,
            Address = new Address2
            {
                AddressLine1 = form.Address.Line1,
                Postcode = form.Address.Postcode,
                City = form.Address.City,
                Country = addressCountry
            },
            Fatca = new Fatca { Status = _settings.Onboarding.Fatca, ConfirmedAt = DateTimeOffset.UtcNow },
            TermsAndConditions = _settings.Onboarding.TermsConsentDocumentId is Guid tc
                ? new TermsAndConditions { ConsentDocumentId = tc, ConfirmedAt = DateTimeOffset.UtcNow }
                : null,
            DataPrivacyAndSharingAgreement = _settings.Onboarding.DataPrivacyConsentDocumentId is Guid dp
                ? new DataPrivacyAndSharingAgreement { ConsentDocumentId = dp, ConfirmedAt = DateTimeOffset.UtcNow }
                : null
        };

        return UserCreateRequest.UserTolCreateRequest(tol);
    }

    private UserCheckCreateRequest BuildKycCheck(InvestorSignupForm form, DocumentType3 documentType, Country addressCountry)
    {
        var method = Method.TryGetKnownValue(_settings.Onboarding.KycMethod, out var m) ? m : Method.IdCollection;

        var kyc = new UserCheckKnowYourCustomerCreateRequest
        {
            CheckConfirmedAt = DateTimeOffset.UtcNow,
            DataDownloadLink = _settings.Onboarding.KycEvidenceUrl,
            DocumentType = documentType,
            DocumentExpirationDate = ToDateTimeOffset(form.IdentityDocument.ExpiryDate),
            Nationality = form.IdentityDocument.IssuingCountry,
            Provider = _settings.Onboarding.KycProvider,
            Method = method,
            ConfirmedAddress = new Address2
            {
                AddressLine1 = form.Address.Line1,
                Postcode = form.Address.Postcode,
                City = form.Address.City,
                Country = addressCountry
            }
        };

        return UserCheckCreateRequest.UserCheckKnowYourCustomerCreateRequest(kyc);
    }

    private static Guid ReadUserId(UserCreateRequest1 response) => response.Match(
        onUserByol: u => u.Id,
        onUserTol: u => u.Id,
        otherwise: _ => throw new UpvestApiException("Upvest returned an unrecognised user creation response.", null));

    // ---- ISO code / enum resolution (caller input validation) ----

    private static Nationality ResolveNationality(string code)
    {
        if (Nationality.TryGetKnownValue(Normalize(code), out var value))
        {
            return value;
        }
        throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 nationality code.");
    }

    private static Country ResolveCountry(string code, string field)
    {
        if (Country.TryGetKnownValue(Normalize(code), out var value))
        {
            return value;
        }
        throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 country code for {field}.");
    }

    private static BirthCountry ResolveBirthCountry(string code)
    {
        if (BirthCountry.TryGetKnownValue(Normalize(code), out var value))
        {
            return value;
        }
        throw new PortfolioValidationException($"'{code}' is not a recognised ISO 3166-1 alpha-2 birth-country code.");
    }

    private static DocumentType3 ResolveDocumentType(string type)
    {
        if (DocumentType3.TryGetKnownValue(Normalize(type), out var value))
        {
            return value;
        }
        throw new PortfolioValidationException(
            $"'{type}' is not a recognised identity-document type (expected PASSPORT, ID_CARD, RESIDENCE_PERMIT, DRIVING_LICENSE or TWO_PLUS_TWO_VERIFICATION_PROOF).");
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
