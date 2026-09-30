using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class PaymentMethodEndpointServices
{
    public PaymentMethodEndpointServices(IRepository<Buyer> buyerRepository, IPayPalPaymentGateway paymentGateway)
    {
        BuyerRepository = buyerRepository;
        PaymentGateway = paymentGateway;
    }

    public IRepository<Buyer> BuyerRepository { get; }
    public IPayPalPaymentGateway PaymentGateway { get; }
}
