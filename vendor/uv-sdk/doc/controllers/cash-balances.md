# Cash Balances

```csharp
CashBalancesApi cashBalancesApi = client.CashBalancesApi;
```

## Class Name

`CashBalancesApi`

## Methods

* [Retrieve Cash Balance](../../doc/controllers/cash-balances.md#retrieve-cash-balance)
* [Retrieve Cash Balances](../../doc/controllers/cash-balances.md#retrieve-cash-balances)
* [Retrieve Cash Balance with Currency](../../doc/controllers/cash-balances.md#retrieve-cash-balance-with-currency)


# Retrieve Cash Balance

**This endpoint is deprecated.**

Retrieve an account group's cash balance

```csharp
RetrieveCashBalanceAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`positions:read`

## Response Type

**200**: Response

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountGroupsPaymentsCashBalanceResponse](../../doc/models/account-groups-payments-cash-balance-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AccountGroupsPaymentsCashBalanceResponse> result = await cashBalancesApi.RetrieveCashBalanceAsync(
        accountGroupId,
        upvestClientId,
        upvestApiVersion
    );
}
catch (ApiException e)
{
    Console.WriteLine(e.Message);
    if (e is ErrorException)
    {
       // TODO: Handle ErrorException exception here
    }
}
```

## Example Response *(as JSON)*

```json
{
  "account_group_id": "d0fc0305-97b7-4e3b-bddf-66d3c434898c",
  "currency": "EUR",
  "balance": "100",
  "locked_for_trading": "10",
  "pending_settlement": "10",
  "available_for_withdrawal": "80",
  "available_for_trading": "80"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Cash Balances

Retrieve an account group's cash balances

```csharp
RetrieveCashBalancesAsync(
    Guid accountGroupId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`positions:read`

## Response Type

**200**: Response

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.CashBalancesRetrieveResponse](../../doc/models/cash-balances-retrieve-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<CashBalancesRetrieveResponse> result = await cashBalancesApi.RetrieveCashBalancesAsync(
        accountGroupId,
        upvestClientId,
        upvestApiVersion
    );
}
catch (ApiException e)
{
    Console.WriteLine(e.Message);
    if (e is ErrorException)
    {
       // TODO: Handle ErrorException exception here
    }
}
```

## Example Response *(as JSON)*

```json
{
  "meta": {
    "count": 2,
    "total_count": 2,
    "offset": 0,
    "limit": 100
  },
  "data": [
    {
      "account_group_id": "d0fc0305-97b7-4e3b-bddf-66d3c434898c",
      "currency": "EUR",
      "balance": "100",
      "locked_for_trading": "10",
      "pending_settlement": "10",
      "available_for_withdrawal": "80",
      "available_for_trading": "80"
    },
    {
      "account_group_id": "d0fc0305-97b7-4e3b-bddf-66d3c434898c",
      "currency": "GBP",
      "balance": "250",
      "locked_for_trading": "0",
      "pending_settlement": "0",
      "available_for_withdrawal": "250",
      "available_for_trading": "250"
    },
    {
      "account_group_id": "d0fc0305-97b7-4e3b-bddf-66d3c434898c",
      "currency": "GBP",
      "balance": "50",
      "locked_for_trading": "10",
      "pending_settlement": "0",
      "available_for_withdrawal": "40",
      "available_for_trading": "40"
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Cash Balance with Currency

Retrieve an account group's cash balance for particular ISO currency code

```csharp
RetrieveCashBalanceWithCurrencyAsync(
    Guid accountGroupId,
    Models.Currency1 currency,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountGroupId` | `Guid` | Template, Required | The unique identifier of the account group. Universally Unique Identifier (UUID). |
| `currency` | [`Currency1`](../../doc/models/currency-1.md) | Template, Required | The [ISO 4217](https://www.iso.org/iso-4217-currency-codes.html) alphabetic currency code identifying the cash balance (e.g. `EUR`). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`positions:read`

## Response Type

**200**: Response

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountGroupsPaymentsCashBalancesResponse](../../doc/models/account-groups-payments-cash-balances-response.md).

## Example Usage

```csharp
Guid accountGroupId = new Guid("00002436-0000-0000-0000-000000000000");
Currency1 currency = Currency1.Gbp;
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AccountGroupsPaymentsCashBalancesResponse> result = await cashBalancesApi.RetrieveCashBalanceWithCurrencyAsync(
        accountGroupId,
        currency,
        upvestClientId,
        upvestApiVersion
    );
}
catch (ApiException e)
{
    Console.WriteLine(e.Message);
    if (e is ErrorException)
    {
       // TODO: Handle ErrorException exception here
    }
}
```

## Example Response *(as JSON)*

```json
{
  "account_group_id": "d0fc0305-97b7-4e3b-bddf-66d3c434898c",
  "currency": "EUR",
  "balance": "100",
  "locked_for_trading": "10",
  "pending_settlement": "10",
  "available_for_withdrawal": "80",
  "available_for_trading": "80"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

