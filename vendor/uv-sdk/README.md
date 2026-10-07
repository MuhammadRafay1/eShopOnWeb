
# Getting Started with Upvest Investment API

## Introduction

Upvest Investment API.

## Install the Package

If you are building with .NET CLI tools then you can also use the following command:

```bash
dotnet add package Up-v-ApimaticSDK --version 0.0.5
```

You can also view the package at:
https://www.nuget.org/packages/Up-v-ApimaticSDK/0.0.5

## Initialize the API Client

**_Note:_** Documentation for the client can be found [here.](doc/client.md)

The following parameters are configurable for the API Client:

| Parameter | Type | Description |
|  --- | --- | --- |
| Environment | [`Environment`](README.md#environments) | The API environment. <br> **Default: `Environment.Production`** |
| Timeout | `TimeSpan` | Http client timeout.<br>*Default*: `TimeSpan.FromSeconds(30)` |
| HttpClientConfiguration | [`Action<HttpClientConfiguration.Builder>`](doc/http-client-configuration-builder.md) | Action delegate that configures the HTTP client by using the HttpClientConfiguration.Builder for customizing API call settings.<br>*Default*: `new HttpClient()` |
| LogBuilder | [`LogBuilder`](doc/log-builder.md) | Represents the logging configuration builder for API calls |
| ClientCredentialsAuth | [`ClientCredentialsAuth`](doc/auth/oauth-2-client-credentials-grant.md) | The Credentials Setter for OAuth 2 Client Credentials Grant |
| HttpSignature | [`HttpSignatureCredentials`](doc/http-signatures.md) | The key material used to sign every request with an HTTP message signature |

The API client can be initialized as follows:

### Code-Based Initialization

```csharp
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;
using UpvestInvestmentApi.Standard.Http.Signature;
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
    .HttpSignature(HttpSignatureCredentials.FromFile(
        keyId: "KeyId",
        privateKeyPath: @"C:\\keys\\upvest-http-sign-key.pem",
        privateKeyPassphrase: "PrivateKeyPassphrase"))
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

### Configuration-Based Initialization

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

See the [Configuration-Based Initialization](doc/configuration-based-initialization.md) section for details.

## Environments

The SDK can be configured to use a different environment for making API calls. Available environments are:

### Fields

| Name | Description |
|  --- | --- |
| Production | **Default** Sandbox environment |
| Environment2 | Live environment |

## Authorization

This API uses the following authentication schemes.

* [`oauth-client-credentials (OAuth 2 Client Credentials Grant)`](doc/auth/oauth-2-client-credentials-grant.md)

## HTTP Signatures

On top of the OAuth 2 token, every request to the Upvest Investment API has to
carry a cryptographic signature. The SDK creates it for you once you pass
`HttpSignature(...)` to the client builder -- see
[HTTP Signatures](doc/http-signatures.md) for details.

## List of APIs

* [Access Tokens](doc/controllers/access-tokens.md)
* [Webhook Subscriptions](doc/controllers/webhook-subscriptions.md)
* [User Identifiers](doc/controllers/user-identifiers.md)
* [User Checks](doc/controllers/user-checks.md)
* [Tax Exemptions](doc/controllers/tax-exemptions.md)
* [Tax Wrappers](doc/controllers/tax-wrappers.md)
* [Account Groups](doc/controllers/account-groups.md)
* [Reference Accounts](doc/controllers/reference-accounts.md)
* [Direct Debits](doc/controllers/direct-debits.md)
* [Credit Fundings](doc/controllers/credit-fundings.md)
* [Cash Balance Transfers](doc/controllers/cash-balance-transfers.md)
* [Virtual Bank Accounts](doc/controllers/virtual-bank-accounts.md)
* [Cash Balances](doc/controllers/cash-balances.md)
* [Price Data](doc/controllers/price-data.md)
* [Tax Residencies](doc/controllers/tax-residencies.md)
* [Fees Configurations](doc/controllers/fees-configurations.md)
* [Transaction Fees Models](doc/controllers/transaction-fees-models.md)
* [Portfolios Rebalancing](doc/controllers/portfolios-rebalancing.md)
* [Virtual Cash Balances](doc/controllers/virtual-cash-balances.md)
* [Savings Plans](doc/controllers/savings-plans.md)
* [Securities Transfers](doc/controllers/securities-transfers.md)
* [Account Transfers](doc/controllers/account-transfers.md)
* [ISA Transfers](doc/controllers/isa-transfers.md)
* [Business Checks](doc/controllers/business-checks.md)
* [Files](doc/controllers/files.md)
* [Users](doc/controllers/users.md)
* [Accounts](doc/controllers/accounts.md)
* [Mandates](doc/controllers/mandates.md)
* [Top-Ups](doc/controllers/top-ups.md)
* [Withdrawals](doc/controllers/withdrawals.md)
* [Instruments](doc/controllers/instruments.md)
* [Orders](doc/controllers/orders.md)
* [Positions](doc/controllers/positions.md)
* [Reports](doc/controllers/reports.md)
* [Transactions](doc/controllers/transactions.md)
* [Fees](doc/controllers/fees.md)
* [Portfolios](doc/controllers/portfolios.md)
* [Valuations](doc/controllers/valuations.md)
* [Liquidations](doc/controllers/liquidations.md)
* [Returns](doc/controllers/returns.md)
* [Tests](doc/controllers/tests.md)
* [Businesses](doc/controllers/businesses.md)
* [Roles](doc/controllers/roles.md)

## SDK Infrastructure

### Configuration

* [Configuration-Based Initialization](doc/configuration-based-initialization.md)
* [HttpClientConfiguration](doc/http-client-configuration.md)
* [HttpClientConfigurationBuilder](doc/http-client-configuration-builder.md)
* [LogBuilder](doc/log-builder.md)
* [LogRequestBuilder](doc/log-request-builder.md)
* [LogResponseBuilder](doc/log-response-builder.md)
* [ProxyConfigurationBuilder](doc/proxy-configuration-builder.md)

### HTTP

* [HttpCallback](doc/http-callback.md)
* [HttpContext](doc/http-context.md)
* [HttpRequest](doc/http-request.md)
* [HttpResponse](doc/http-response.md)
* [HttpStringResponse](doc/http-string-response.md)
* [HTTP Signatures](doc/http-signatures.md)

### Utilities

* [ApiException](doc/api-exception.md)
* [ApiResponse](doc/api-response.md)
* [ApiHelper](doc/api-helper.md)
* [CustomDateTimeConverter](doc/custom-date-time-converter.md)
* [UnixDateTimeConverter](doc/unix-date-time-converter.md)

