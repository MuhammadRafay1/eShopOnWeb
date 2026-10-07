
# OAuth 2 Client Credentials Grant



Documentation for accessing and setting credentials for oauth-client-credentials.

## Auth Credentials

| Name | Type | Description | Setter | Getter |
|  --- | --- | --- | --- | --- |
| OauthClientId | `string` | OAuth 2 Client ID | `OauthClientId` | `OauthClientId` |
| OauthClientSecret | `string` | OAuth 2 Client Secret | `OauthClientSecret` | `OauthClientSecret` |
| OauthToken | `Models.OauthToken` | Object for storing information about the OAuth token | `OauthToken` | `OauthToken` |
| OauthScopes | `List<Models.OauthScope>` | List of scopes that apply to the OAuth token | `OauthScopes` | `OauthScopes` |
| OauthClockSkew | `TimeSpan?` | Clock skew time in seconds applied while checking the OAuth Token expiry. | `OauthClockSkew` | `OauthClockSkew` |
| OauthTokenProvider | `Func<ClientCredentialsAuthManager, OauthToken, Task<OauthToken>>` | Registers a callback for oAuth Token Provider used for automatic token fetching/refreshing. | `OauthTokenProvider` | `OauthTokenProvider` |
| OauthOnTokenUpdate | `Action<OauthToken>` | Registers a callback for token update event. | `OauthOnTokenUpdate` | `OauthOnTokenUpdate` |



**Note:** Auth credentials can be set using `ClientCredentialsAuth` in the client builder and accessed through `ClientCredentialsAuth` method in the client instance.

## Usage Example

### Client Initialization

You must initialize the client with *OAuth 2.0 Client Credentials Grant* credentials as shown in the following code snippet. This will fetch the OAuth token automatically when any of the endpoints, requiring *OAuth 2.0 Client Credentials Grant* authentication, are called.

```csharp
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;

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
    .Build();
```



Your application can also manually provide an OAuthToken using the setter `oauthToken` in `ClientCredentialsAuthModel` object. This function takes in an instance of OAuthToken containing information for authorizing client requests and refreshing the token itself.

You must have initialized the client with scopes for which you need permission to access.

### Scopes

Scopes enable your application to only request access to the resources it needs while enabling users to control the amount of access they grant to your application. Available scopes are defined in the [`OauthScope`](../../doc/models/oauth-scope.md) enumeration.

| Scope Name | Description |
|  --- | --- |
| `ACCOUNTSREAD` | Read accounts and account groups |
| `ACCOUNTSADMIN` | Create/update/delete accounts and account groups |
| `WEBHOOKSADMIN` | Create/update/delete webhooks |
| `WEBHOOKSREAD` | Read webhooks |
| `FILESREAD` | Read files metadata |
| `ORDERSADMIN` | Create/update/delete orders |
| `ORDERSREAD` | Read orders |
| `USERSADMIN` | Create/update/delete users |
| `USERSREAD` | Read users |
| `CHECKSADMIN` | Create checks |
| `CHECKSREAD` | Read checks |
| `POSITIONSREAD` | Read positions |
| `POSITIONS_PERFORMANCEREAD` | Read positions performance |
| `REFERENCE_ACCOUNTSADMIN` | Create/update/delete reference accounts |
| `REFERENCE_ACCOUNTSREAD` | Read reference accounts |
| `MANDATESADMIN` | Create/update/delete mandates |
| `MANDATESREAD` | Read mandates |
| `PAYMENTSADMIN` | Payins and withdrawal operations |
| `PAYMENTSREAD` | Payins and withdrawal read operations |
| `TOPUPSADMIN` | Top-ups operations |
| `TOPUPSREAD` | Top-ups read operations |
| `CASH_BALANCE_TRANSFERSADMIN` | Cash balance transfer operations |
| `CASH_BALANCE_TRANSFERSREAD` | Cash balance transfer read operations |
| `CREDIT_FUNDINGSREAD` | Credit Fundings read operations |
| `SECURITIES_TRANSFERSREAD` | Securities Transfers read transfers |
| `SECURITIES_TRANSFERSADMIN` | Securities Transfers operations |
| `ISA_TRANSFERSADMIN` | ISA Transfers operations |
| `REPORTSREAD` | Read reports |
| `REPORTSADMIN` | Create reports |
| `TAXESREAD` | Read tax residencies |
| `TAXESADMIN` | Modify tax residencies and tax exemptions |
| `TRANSACTIONSREAD` | Read cash and securities transactions |
| `INSTRUMENTSREAD` | Read instruments |
| `FEESADMIN` | Create and read fee operations |
| `FEESREAD` | Read fee operations |
| `TRANSACTION_FEESADMIN` | Create and read transaction fee operations |
| `TRANSACTION_FEESREAD` | Read transaction fee operations |
| `PORTFOLIOSREAD` | Read portfolios |
| `PORTFOLIOSADMIN` | Modify portfolios |
| `VALUATIONSREAD` | Read valuations |
| `ACCOUNT_LIQUIDATIONSREAD` | Read accounts liquidations |
| `ACCOUNT_LIQUIDATIONSADMIN` | Trigger/read/cancel accounts liquidations |
| `ACCOUNT_RETURNSREAD` | Read accounts returns |
| `VIRTUAL_CASH_BALANCESADMIN` | Virtual cash balances |
| `SAVINGS_PLANSREAD` | Read savings plans |
| `SAVINGS_PLANSADMIN` | Create/read savings plans |
| `PRICESREAD` | Read instrument prices, |
| `TESTSADMIN` | Testing related operations |
| `ACCOUNT_TRANSFERSREAD` | Account Transfers read transfers |
| `ACCOUNT_TRANSFERSADMIN` | Account Transfers operations |
| `BUSINESSESADMIN` | Create/update/delete businesses, |
| `BUSINESSESREAD` | Read businesses, |
| `ROLESADMIN` | Create/update/deactivate roles |
| `ROLESREAD` | Read roles |
| `CORPORATE_ACTIONSADMIN` | Create/update/delete corporate action instructions |

### Adding OAuth Token Update Callback

Whenever the OAuth Token gets updated, the provided callback implementation will be executed. For instance, you may use it to store your access token whenever it gets updated.

```csharp
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;

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
        .OauthOnTokenUpdate(token => 
        {
            // It will be triggered whenever the token gets updated
            SaveTokenToDatabase(token);
        })
        .Build())
    .Build();
```

### Adding Custom OAuth Token Provider

To authorize a client using a stored access token, set up the `oauthTokenProvider` in `ClientCredentialsAuthModel` builder along with the other auth parameters before creating the client:

```csharp
using UpvestInvestmentApi.Standard;
using UpvestInvestmentApi.Standard.Authentication;

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
        .OauthTokenProvider(async (credentialsManager, token) =>
        {
            // Add the callback handler to provide a new OAuth token
            // It will be triggered whenever the token is undefined or expired
            return LoadTokenFromDatabase() ?? await credentialsManager.FetchTokenAsync();
        })
        .Build())
    .Build();
```


