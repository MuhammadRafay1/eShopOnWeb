using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>Bundles the services the order endpoints need behind a single DI-friendly type.</summary>
public class OrderEndpointServices
{
    public OrderEndpointServices(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<CatalogItem> catalogItemRepository,
        IRepository<Buyer> buyerRepository,
        IPayPalPaymentGateway paymentGateway,
        IUriComposer uriComposer)
    {
        OrderRepository = orderRepository;
        PaymentRepository = paymentRepository;
        CatalogItemRepository = catalogItemRepository;
        BuyerRepository = buyerRepository;
        PaymentGateway = paymentGateway;
        UriComposer = uriComposer;
    }

    public IRepository<Order> OrderRepository { get; }
    public IRepository<Payment> PaymentRepository { get; }
    public IRepository<CatalogItem> CatalogItemRepository { get; }
    public IRepository<Buyer> BuyerRepository { get; }
    public IPayPalPaymentGateway PaymentGateway { get; }
    public IUriComposer UriComposer { get; }
}
