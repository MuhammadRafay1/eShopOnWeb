
# Client Class Documentation

The following parameters are configurable for the API Client:

| Parameter | Type | Description |
|  --- | --- | --- |
| Environment | [`Environment`](../README.md#environments) | The API environment. <br> **Default: `Environment.Production`** |
| Timeout | `TimeSpan` | Http client timeout.<br>*Default*: `TimeSpan.FromSeconds(30)` |
| HttpClientConfiguration | [`Action<HttpClientConfiguration.Builder>`](../doc/http-client-configuration-builder.md) | Action delegate that configures the HTTP client by using the HttpClientConfiguration.Builder for customizing API call settings.<br>*Default*: `new HttpClient()` |
| LogBuilder | [`LogBuilder`](../doc/log-builder.md) | Represents the logging configuration builder for API calls |
| ClientCredentialsAuth | [`ClientCredentialsAuth`](auth/oauth-2-client-credentials-grant.md) | The Credentials Setter for OAuth 2 Client Credentials Grant |

The API client can be initialized as follows:

## Code-Based Initialization

```csharp
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Models;

namespace ConsoleApp;

UpvestInvestmentApiClient client = new UpvestInvestmentApiClient.Builder()
    .ClientCredentialsAuth(
        new ClientCredentialsAuthModel.Builder(
            "OAuthClientId",
            "OAuthClientSecret"
        )
        .OauthScopes(
            new List<OauthScope>
            {
                OauthScope.Accountsread,
                OauthScope.Accountsadmin,
            })
        .Build())
    .HttpClientConfig(httpClientConfig =>
        httpClientConfig.Timeout(TimeSpan.FromSeconds(100)))
    .Environment(UpvestInvestmentApi.Standard.Environment.Production)
    .LoggingConfig(config => config
        .LogLevel(LogLevel.Information)
        .RequestConfig(reqConfig => reqConfig.Body(true))
        .ResponseConfig(respConfig => respConfig.Headers(true))
    )
    .Build();
```

## Configuration-Based Initialization

```csharp
using UpvestInvestmentApi.Standard;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp;

// Build the IConfiguration using .NET conventions (JSON, environment, etc.)
var configuration = new ConfigurationBuilder()
    .AddJsonFile("config.json")
    .AddEnvironmentVariables() // [optional] read environment variables
    .Build();

// Instantiate your SDK and configure it from IConfiguration
var client = UpvestInvestmentApiClient
    .FromConfiguration(configuration.GetSection("UpvestInvestmentApi"));
```

See the [Configuration-Based Initialization](../doc/configuration-based-initialization.md) section for details.

## Upvest Investment APIClient Class

The gateway for the SDK. This class acts as a factory for the Apis and also holds the configuration of the SDK.

### Controllers

| Name | Description |
|  --- | --- |
| AccessTokensApi | Gets AccessTokensApi. |
| FilesApi | Gets FilesApi. |
| WebhookSubscriptionsApi | Gets WebhookSubscriptionsApi. |
| UsersApi | Gets UsersApi. |
| UserIdentifiersApi | Gets UserIdentifiersApi. |
| UserChecksApi | Gets UserChecksApi. |
| AccountsApi | Gets AccountsApi. |
| TaxExemptionsApi | Gets TaxExemptionsApi. |
| TaxWrappersApi | Gets TaxWrappersApi. |
| AccountGroupsApi | Gets AccountGroupsApi. |
| ReferenceAccountsApi | Gets ReferenceAccountsApi. |
| MandatesApi | Gets MandatesApi. |
| DirectDebitsApi | Gets DirectDebitsApi. |
| CreditFundingsApi | Gets CreditFundingsApi. |
| TopUpsApi | Gets TopUpsApi. |
| CashBalanceTransfersApi | Gets CashBalanceTransfersApi. |
| WithdrawalsApi | Gets WithdrawalsApi. |
| VirtualBankAccountsApi | Gets VirtualBankAccountsApi. |
| CashBalancesApi | Gets CashBalancesApi. |
| InstrumentsApi | Gets InstrumentsApi. |
| PriceDataApi | Gets PriceDataApi. |
| OrdersApi | Gets OrdersApi. |
| PositionsApi | Gets PositionsApi. |
| ReportsApi | Gets ReportsApi. |
| TaxResidenciesApi | Gets TaxResidenciesApi. |
| TransactionsApi | Gets TransactionsApi. |
| FeesApi | Gets FeesApi. |
| FeesConfigurationsApi | Gets FeesConfigurationsApi. |
| TransactionFeesModelsApi | Gets TransactionFeesModelsApi. |
| PortfoliosApi | Gets PortfoliosApi. |
| PortfoliosRebalancingApi | Gets PortfoliosRebalancingApi. |
| ValuationsApi | Gets ValuationsApi. |
| LiquidationsApi | Gets LiquidationsApi. |
| ReturnsApi | Gets ReturnsApi. |
| VirtualCashBalancesApi | Gets VirtualCashBalancesApi. |
| SavingsPlansApi | Gets SavingsPlansApi. |
| TestsApi | Gets TestsApi. |
| SecuritiesTransfersApi | Gets SecuritiesTransfersApi. |
| AccountTransfersApi | Gets AccountTransfersApi. |
| IsaTransfersApi | Gets IsaTransfersApi. |
| BusinessesApi | Gets BusinessesApi. |
| BusinessChecksApi | Gets BusinessChecksApi. |
| RolesApi | Gets RolesApi. |
| OauthAuthorizationApi | Gets OauthAuthorizationApi. |

### Properties

| Name | Description | Type |
|  --- | --- | --- |
| HttpClientConfiguration | Gets the configuration of the Http Client associated with this client. | [`IHttpClientConfiguration`](../doc/http-client-configuration.md) |
| Timeout | Http client timeout. | `TimeSpan` |
| Environment | Current API environment. | `Environment` |
| ClientCredentialsAuth | Gets the credentials to use with ClientCredentialsAuth. | [`IClientCredentialsAuth`](auth/oauth-2-client-credentials-grant.md) |

### Methods

| Name | Description | Return Type |
|  --- | --- | --- |
| `GetBaseUri(Server alias = Server.Default)` | Gets the URL for a particular alias in the current environment and appends it with template parameters. | `string` |
| `ToBuilder()` | Creates an object of the Upvest Investment APIClient using the values provided for the builder. | `Builder` |

## Upvest Investment APIClient Builder Class

Class to build instances of Upvest Investment APIClient.

### Methods

| Name | Description | Return Type |
|  --- | --- | --- |
| `HttpClientConfiguration(Action<`[`HttpClientConfiguration.Builder`](../doc/http-client-configuration-builder.md)`> action)` | Gets the configuration of the Http Client associated with this client. | `Builder` |
| `Timeout(TimeSpan timeout)` | Http client timeout. | `Builder` |
| `Environment(Environment environment)` | Current API environment. | `Builder` |
| `ClientCredentialsAuth(Action<ClientCredentialsAuthModel.Builder> action)` | Sets credentials for ClientCredentialsAuth. | `Builder` |

