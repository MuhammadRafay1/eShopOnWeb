using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class OrderPaymentsByOrderIdsSpecification : Specification<OrderPayment>
{
    public OrderPaymentsByOrderIdsSpecification(int[] orderIds)
    {
        Query.Where(p => orderIds.Contains(p.OrderId));
    }
}
