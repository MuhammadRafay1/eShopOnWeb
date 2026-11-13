using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Data;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;
using Microsoft.eShopWeb.PublicApi.Portfolio.Upvest;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Services;

/// <summary>The command carrying the whole sign-up form, including the identity document the shop verified.</summary>
public sealed record InvestorSignupCommand(
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset BirthDate,
    string? BirthCity,
    string? BirthCountry,
    string Nationality,
    string AddressLine1,
    string Postcode,
    string City,
    string AddressCountry,
    string? PhoneNumber,
    string? TaxId,
    string? TaxCountry,
    string IdentityDocumentType,
    string IdentityDocumentNumber,
    string IdentityDocumentIssuingCountry,
    DateTimeOffset IdentityDocumentExpiryDate);

/// <summary>The caller-facing investor state: the Upvest investor id (if created) and the sign-up status.</summary>
public sealed record InvestorStatusResult(Guid? InvestorId, string Status);

/// <summary>
/// Flow 1 — becoming an investor, and choosing a model. Owns the investor-link record and keeps its status reconciled
/// with Upvest. All lookups are keyed by the signed-in shopper, so one shopper never sees another's data.
/// </summary>
public sealed class InvestorService
{
    private readonly PortfolioContext _db;
    private readonly IUpvestGateway _gateway;
    private readonly ILogger<InvestorService> _logger;

    public InvestorService(PortfolioContext db, IUpvestGateway gateway, ILogger<InvestorService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<InvestorStatusResult> SignUpAsync(string shopperId, InvestorSignupCommand cmd, CancellationToken ct)
    {
        // Claim first, keyed by the shopper. A concurrent/duplicate sign-up loses the primary-key race; we then recover
        // the existing record and (re-)provision it idempotently, so a partial or unknown-outcome first attempt is
        // completed rather than duplicated.
        var record = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (record is null)
        {
            record = new InvestorRecord
            {
                ShopperId = shopperId,
                IdentityDocumentType = cmd.IdentityDocumentType,
                IdentityDocumentNumber = cmd.IdentityDocumentNumber,
                IdentityDocumentIssuingCountry = cmd.IdentityDocumentIssuingCountry,
                IdentityDocumentExpiryDate = cmd.IdentityDocumentExpiryDate.UtcDateTime,
                TaxId = cmd.TaxId,
                TaxCountry = cmd.TaxCountry,
            };
            try
            {
                _db.Investors.Add(record);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
            {
                _db.Entry(record).State = EntityState.Detached;
                record = await _db.Investors.FirstAsync(i => i.ShopperId == shopperId, ct);
            }
        }

        await ProvisionAsync(record, cmd, ct);
        return new InvestorStatusResult(record.UpvestUserId, ToWire(record.Status));
    }

    /// <summary>
    /// Brings an investor record up to a ready account at Upvest, doing only the steps not yet done. Each write reuses
    /// the record's persisted idempotency key, so a retry (duplicate sign-up, or recovery after an unknown outcome) is a
    /// safe replay, not a duplicate. Scope-refused / unknown-outcome steps leave the investor PENDING.
    /// </summary>
    private async Task ProvisionAsync(InvestorRecord record, InvestorSignupCommand cmd, CancellationToken ct)
    {
        string userStatus = "ACTIVE";

        if (record.UpvestUserId is null)
        {
            var details = new InvestorSignupDetails(
                cmd.FirstName, cmd.LastName, cmd.Email, cmd.BirthDate, cmd.BirthCity, cmd.BirthCountry,
                cmd.Nationality, cmd.AddressLine1, cmd.Postcode, cmd.City, cmd.AddressCountry, cmd.PhoneNumber, cmd.TaxCountry);
            try
            {
                var user = await _gateway.CreateUserAsync(details, record.UserIdempotencyKey, ct);
                record.UpvestUserId = user.UserId;
                userStatus = user.Status;
                record.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (UpvestProviderException ex) when (ex.OutcomeUnknown)
            {
                // The connection failed after the provider may have created the user. Keep the claim and stay pending;
                // a later sign-up retry replays CreateUser with the same idempotency key to settle the outcome.
                _logger.LogWarning("Investor {ShopperId}: user creation outcome unknown; staying pending for retry.", record.ShopperId);
                record.Status = InvestorStatus.Pending;
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (UpvestProviderException)
            {
                // Definite provider rejection (bad data / unauthorised / 5xx): release the claim so the shopper can fix and retry.
                _db.Investors.Remove(record);
                await _db.SaveChangesAsync(ct);
                throw;
            }
        }

        var userId = record.UpvestUserId.Value;

        // Account group then holding account. In the sandbox these require scopes this client lacks, so an
        // unauthorised/transport failure leaves the investor PENDING rather than failing the whole sign-up.
        try
        {
            if (record.AccountGroupId is null)
            {
                record.AccountGroupId = await _gateway.CreateAccountGroupAsync(userId, record.AccountGroupIdempotencyKey, ct);
                record.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            if (record.AccountId is null)
            {
                var account = await _gateway.CreateAccountAsync(userId, record.AccountGroupId.Value, record.AccountIdempotencyKey, ct);
                record.AccountId = account.AccountId;
                record.AccountGroupId = account.AccountGroupId;
                record.Status = MapStatus(userStatus, account.Status);
                record.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (UpvestProviderException ex)
        {
            _logger.LogWarning("Investor {ShopperId}: account provisioning not completed ({Reason}); staying pending.",
                record.ShopperId, ex.UnauthorizedScope ? "unauthorised scope" : (ex.OutcomeUnknown ? "unknown outcome" : $"status {(int?)ex.StatusCode}"));
            record.Status = InvestorStatus.Pending;
            record.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<InvestorStatusResult?> GetStatusAsync(string shopperId, CancellationToken ct)
    {
        var record = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct);
        if (record is null)
            return null;

        if (record.UpvestUserId is { } userId)
        {
            try
            {
                var user = await _gateway.GetUserAsync(userId, ct);
                string? accountStatus = null;
                if (record.AccountId is { } accountId)
                    accountStatus = await _gateway.GetPortfolioAccountStatusAsync(userId, accountId, ct);

                record.Status = MapStatus(user.Status, accountStatus);
                record.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Investor {ShopperId}: status could not be refreshed ({Status}); reporting last-known.",
                    shopperId, (int?)ex.StatusCode);
            }
        }

        return new InvestorStatusResult(record.UpvestUserId, ToWire(record.Status));
    }

    public async Task ChooseModelAsync(string shopperId, string modelId, CancellationToken ct)
    {
        var record = await _db.Investors.FirstOrDefaultAsync(i => i.ShopperId == shopperId, ct)
            ?? throw new PortfolioRequestException("You are not registered as an investor.", 400);

        var model = await _db.Models.FirstOrDefaultAsync(m => m.ModelId == modelId, ct)
            ?? throw new PortfolioRequestException($"Model '{modelId}' does not exist.", 404);

        var switching = record.ChosenModelId is not null && record.ChosenModelId != modelId;
        record.ChosenModelId = modelId;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Link the investor's account to the model's allocation at Upvest when both ids are known. In the sandbox this
        // needs scopes the client lacks; the local choice stands either way.
        if (record.AccountId is { } accountId && model.AllocationId is { } allocationId)
        {
            try
            {
                if (switching)
                    await _gateway.UpdateConfigurationAsync(accountId, allocationId, Guid.NewGuid(), ct);
                else
                    await _gateway.CreateConfigurationAsync(accountId, allocationId, Guid.NewGuid(), ct);
            }
            catch (UpvestProviderException ex)
            {
                _logger.LogWarning("Investor {ShopperId}: portfolio configuration not applied at Upvest ({Status}); local choice recorded.",
                    shopperId, (int?)ex.StatusCode);
            }
        }
    }

    internal static InvestorStatus MapStatus(string userStatus, string? accountStatus)
    {
        if (userStatus is "OFFBOARDED" or "OFFBOARDING")
            return InvestorStatus.Rejected;
        if (accountStatus is "CLOSED" or "LOCKED")
            return InvestorStatus.Rejected;
        if (accountStatus == "ACTIVE")
            return InvestorStatus.Active;
        return InvestorStatus.Pending;
    }

    internal static string ToWire(InvestorStatus status) => status switch
    {
        InvestorStatus.Active => "active",
        InvestorStatus.Rejected => "rejected",
        _ => "pending",
    };
}
