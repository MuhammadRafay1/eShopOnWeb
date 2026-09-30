using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// A single saved card scoped to its owning buyer - returns nothing for another shopper's card id.
/// </summary>
public class SavedCardByIdAndBuyerSpecification : Specification<SavedCard>
{
    public SavedCardByIdAndBuyerSpecification(int id, string buyerId)
    {
        Query.Where(c => c.Id == id && c.BuyerId == buyerId);
    }
}
