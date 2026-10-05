using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

public sealed class EnrolmentService : IEnrolmentService
{
    private readonly CatalogContext _db;
    private readonly IUpvestClient _upvest;
    private readonly ILogger<EnrolmentService> _logger;

    public EnrolmentService(CatalogContext db, IUpvestClient upvest, ILogger<EnrolmentService> logger)
    {
        _db = db;
        _upvest = upvest;
        _logger = logger;
    }

    public async Task<Investor> EnrolAsync(string buyerId, EnrolmentDetails details, CancellationToken ct)
    {
        var existing = await _db.Investors.FirstOrDefaultAsync(i => i.BuyerId == buyerId, ct);
        if (existing is not null)
        {
            // Already opted in — enrolment is idempotent.
            return existing;
        }

        var investor = new Investor(buyerId);

        // Register the shopper as an investor with Upvest. Checks run async at
        // Upvest; the user then activates, which reconciliation picks up.
        var user = await _upvest.CreateUserAsync(details, ct);
        investor.LinkUpvestUser(user.Id);
        await _upvest.SubmitKycCheckAsync(user.Id, details, ct);
        await _upvest.SubmitInstrumentFitCheckAsync(user.Id, ct);
        await _upvest.SetTaxResidenciesAsync(user.Id, details.TaxCountry, details.TaxId, ct);

        _db.Investors.Add(investor);
        await _db.SaveChangesAsync(ct);

        // Note: no shopper personal data is logged.
        _logger.LogInformation("Enrolment {InvestorId} submitted to Upvest; awaiting acceptance.", investor.Id);
        return investor;
    }

    public Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken ct) =>
        _db.Investors.FirstOrDefaultAsync(i => i.BuyerId == buyerId, ct)!;
}
