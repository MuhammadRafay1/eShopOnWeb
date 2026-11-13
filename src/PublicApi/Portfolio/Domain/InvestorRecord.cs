using System;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

/// <summary>
/// The link between an eShop shopper and their Upvest investor identity, plus the shop's own record of the
/// identity/tax details it collected and verified at sign-up. One row per shopper; a shopper only ever sees
/// their own row (lookups are keyed by <see cref="ShopperId"/>).
/// </summary>
public class InvestorRecord
{
    /// <summary>The signed-in eShop shopper's identity (the JWT name claim). Primary key — one investor per shopper.</summary>
    public string ShopperId { get; set; } = string.Empty;

    /// <summary>Upvest user id, once the user has been created. This is the <c>investorId</c> surfaced by the API.</summary>
    public Guid? UpvestUserId { get; set; }

    /// <summary>Upvest account-group id, once created.</summary>
    public Guid? AccountGroupId { get; set; }

    /// <summary>Upvest account id that holds the managed portfolio, once created.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Last-known sign-up status, reconciled from Upvest.</summary>
    public InvestorStatus Status { get; set; } = InvestorStatus.Pending;

    /// <summary>The local model id (see <see cref="ModelPortfolio"/>) the investor has chosen to follow, if any.</summary>
    public string? ChosenModelId { get; set; }

    // Persisted idempotency keys so a retry of a sign-up write reuses the same key rather than minting a new one.
    public Guid UserIdempotencyKey { get; set; } = Guid.NewGuid();
    public Guid AccountGroupIdempotencyKey { get; set; } = Guid.NewGuid();
    public Guid AccountIdempotencyKey { get; set; } = Guid.NewGuid();

    // The shop's record of the identity document it checked and the tax details collected on the sign-up form.
    // Held locally (the TOL user-create contract has no members for these). Personal data: never logged.
    public string? IdentityDocumentType { get; set; }
    public string? IdentityDocumentNumber { get; set; }
    public string? IdentityDocumentIssuingCountry { get; set; }
    public DateTime? IdentityDocumentExpiryDate { get; set; }
    public string? TaxId { get; set; }
    public string? TaxCountry { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
