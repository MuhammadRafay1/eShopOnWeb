using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using Microsoft.Extensions.Logging;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.AnyOf;
using UpvestInvestmentApi.Models.Enums;
using UpvestInvestmentApi.Requests.Instruments;
using UpvestInvestmentApi.Requests.Portfolios;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

public partial class UpvestPortfolioService
{
    private const int MaxInstrumentPages = 100;

    // Flow 2 — model portfolios ---------------------------------------------------------------------

    public async Task<ModelPortfolioResult> UpsertModelAsync(
        string modelId, ModelPortfolioDefinition definition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new PortfolioValidationException("A model id is required.");
        }
        if (definition.Funds is null || definition.Funds.Count == 0)
        {
            throw new PortfolioValidationException("A model portfolio must contain at least one fund.");
        }

        ValidateWeights(definition.Funds);

        // Build the Upvest allocation entries, validating each ISIN against the tradable instrument set.
        await ValidateInstrumentsAsync(definition.Funds.Select(f => f.InstrumentId), cancellationToken);

        var allocationEntries = definition.Funds
            .Select(f => new Allocation
            {
                InstrumentId = InstrumentId.String(Normalize(f.InstrumentId)),
                InstrumentIdType = InstrumentIdType4.Isin,
                Weight = FormatWeight(f.Weight)
            })
            .ToArray();

        var existing = await _repository.GetModelAsync(modelId, cancellationToken);

        Guid allocationId;
        if (existing is null)
        {
            var response = await CallAsync("CreatePortfoliosAllocation", c => _client.Portfolios.CreatePortfoliosAllocation(
                new CreatePortfoliosAllocationRequest
                {
                    IdempotencyKey = DeterministicKey($"allocation-create:{modelId}"),
                    Body = new PortfoliosAllocationCreateRequest { Name = definition.Name, Allocation = allocationEntries }
                }, cancellationToken: c), cancellationToken);
            allocationId = response.Id;
        }
        else
        {
            // PUT is a full replace: every fund is sent, so omitted funds are removed.
            var response = await CallAsync("UpdatePortfoliosAllocation", c => _client.Portfolios.UpdatePortfoliosAllocation(
                new UpdatePortfoliosAllocationRequest
                {
                    AllocationId = existing.AllocationId,
                    Body = new PortfoliosAllocationUpdateRequest { Name = definition.Name, Allocation = allocationEntries }
                }, cancellationToken: c), cancellationToken);
            allocationId = response.Id;
        }

        var record = new ModelPortfolioRecord
        {
            ModelId = modelId,
            AllocationId = allocationId,
            Name = definition.Name,
            UpdatedAt = DateTimeOffset.UtcNow,
            Funds = definition.Funds
                .Select(f => new ModelFund { InstrumentId = Normalize(f.InstrumentId), Weight = f.Weight, ModelId = modelId })
                .ToList()
        };
        await _repository.UpsertModelAsync(record, cancellationToken);

        return ToResult(record);
    }

    public async Task<IReadOnlyList<ModelPortfolioResult>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var models = await _repository.ListModelsAsync(cancellationToken);
        return models.Select(ToResult).ToList();
    }

    public async Task ChooseModelAsync(string shopper, string modelId, CancellationToken cancellationToken)
    {
        var investor = await _repository.GetInvestorAsync(shopper, cancellationToken);
        if (investor is null)
        {
            throw new InvestorNotFoundException();
        }

        var model = await _repository.GetModelAsync(modelId, cancellationToken);
        if (model is null)
        {
            throw new ModelNotFoundException(modelId);
        }

        if (investor.UpvestAccountId is not Guid accountId)
        {
            throw new InvestorNotReadyException(
                "The investor's portfolio account is not ready yet; a model can be chosen once the account is active.");
        }

        // Link the investor's account to the model's allocation (create the configuration, or switch it).
        var hasConfiguration = await HasConfigurationAsync(accountId, cancellationToken);
        if (hasConfiguration)
        {
            await CallAsync("UpdatePortfoliosConfiguration", c => _client.Portfolios.UpdatePortfoliosConfiguration(
                new UpdatePortfoliosConfigurationRequest
                {
                    AccountId = accountId,
                    IdempotencyKey = DeterministicKey($"config-update:{shopper}:{modelId}"),
                    Body = new PortfoliosConfigurationUpdateRequest { AllocationId = model.AllocationId }
                }, cancellationToken: c), cancellationToken);
        }
        else
        {
            await CallAsync("CreatePortfoliosConfiguration", c => _client.Portfolios.CreatePortfoliosConfiguration(
                new CreatePortfoliosConfigurationRequest
                {
                    IdempotencyKey = DeterministicKey($"config-create:{shopper}:{modelId}"),
                    Body = new PortfoliosConfigurationCreateRequest { AccountId = accountId, AllocationId = model.AllocationId }
                }, cancellationToken: c), cancellationToken);
        }

        investor.ChosenModelId = modelId;
        await _repository.UpdateInvestorAsync(investor, cancellationToken);
    }

    private async Task<bool> HasConfigurationAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            await CallAsync("RetrievePortfoliosConfiguration", c => _client.Portfolios.RetrievePortfoliosConfiguration(
                new RetrievePortfoliosConfigurationRequest { AccountId = accountId }, cancellationToken: c), cancellationToken);
            return true;
        }
        catch (UpvestApiException ex) when (ex.ProviderStatusCode == 404)
        {
            return false;
        }
    }

    /// <summary>Validate every ISIN against the tradable instrument set. Tolerates the sandbox scope limit
    /// (401/403) by skipping the check, but any other provider error propagates.</summary>
    private async Task ValidateInstrumentsAsync(IEnumerable<string> isins, CancellationToken cancellationToken)
    {
        var wanted = isins.Select(Normalize).Where(s => s.Length > 0).ToHashSet();
        if (wanted.Count == 0)
        {
            return;
        }

        HashSet<string> known;
        try
        {
            known = await LoadKnownIsinsAsync(cancellationToken);
        }
        catch (UpvestApiException ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning("Skipping ISIN validation: the Upvest client lacks the instruments scope ({Status}).", ex.ProviderStatusCode);
            return;
        }

        var unknown = wanted.Where(i => !known.Contains(i)).ToList();
        if (unknown.Count > 0)
        {
            throw new PortfolioValidationException(
                $"These fund ISINs are not tradable Upvest instruments: {string.Join(", ", unknown)}.");
        }
    }

    private async Task<HashSet<string>> LoadKnownIsinsAsync(CancellationToken cancellationToken)
    {
        var known = new HashSet<string>();
        int offset = 0;
        const int limit = 100;

        for (var page = 0; page < MaxInstrumentPages; page++)
        {
            var currentOffset = offset;
            var response = await CallAsync("ListInstruments", c => _client.Instruments.ListInstruments(
                new ListInstrumentsRequest { Offset = currentOffset, Limit = limit }, cancellationToken: c), cancellationToken);

            foreach (var instrument in response.Data)
            {
                if (!string.IsNullOrWhiteSpace(instrument.Isin))
                {
                    known.Add(Normalize(instrument.Isin));
                }
            }

            offset += response.Data.Count;
            if (response.Data.Count < limit || offset >= response.Meta.TotalCount)
            {
                break;
            }
        }

        return known;
    }

    private static void ValidateWeights(IReadOnlyList<ModelFundInput> funds)
    {
        decimal sum = 0m;
        foreach (var fund in funds)
        {
            if (string.IsNullOrWhiteSpace(fund.InstrumentId))
            {
                throw new PortfolioValidationException("Every fund must carry an instrumentId (ISIN).");
            }
            if (fund.Weight <= 0m || fund.Weight > 100m)
            {
                throw new PortfolioValidationException($"Fund weight for '{fund.InstrumentId}' must be greater than 0 and at most 100.");
            }
            sum += fund.Weight;
        }

        if (sum != 100m)
        {
            throw new PortfolioValidationException($"Fund weights must add up to exactly 100 (they add up to {sum}).");
        }
    }

    private static ModelPortfolioResult ToResult(ModelPortfolioRecord record) =>
        new(record.ModelId, record.Name,
            record.Funds.Select(f => new ModelFundInput(f.InstrumentId, f.Weight)).ToList());
}
