# Liquidations

All accounts liquidations related paths.

```csharp
LiquidationsApi liquidationsApi = client.LiquidationsApi;
```

## Class Name

`LiquidationsApi`

## Methods

* [List Accounts Liquidations](../../doc/controllers/liquidations.md#list-accounts-liquidations)
* [Create Account Liquidation](../../doc/controllers/liquidations.md#create-account-liquidation)
* [Retrieve Account Liquidation](../../doc/controllers/liquidations.md#retrieve-account-liquidation)
* [Cancel Account Liquidation](../../doc/controllers/liquidations.md#cancel-account-liquidation)


# List Accounts Liquidations

Returns a paginated list of account liquidation requests for the authenticated client.

See the Liquidations guide ([TOL](https://docs.upvest.co/products/tol/guides/liquidations) / [BYOL](https://docs.upvest.co/products/byol/guides/liquidations)) for implementation details.

```csharp
ListAccountsLiquidationsAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort23? sort = Models.Sort23.Id,
    Models.Order58? order = Models.Order58.Asc,
    int? offset = null,
    int? limit = 100,
    string startDate = null,
    string endDate = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort23?`](../../doc/models/sort-23.md) | Query, Optional | Sort the result by `id`.<br><br>**Default**: `Sort23.id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `startDate` | `string` | Query, Optional | Returns accounts liquidations created starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns accounts liquidations created up until this date (UTC) |

## Requires scope

### oauth-client-credentials

`account_liquidations:admin`, `account_liquidations:read`

## Response Type

**200**: Accounts liquidations

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosOrdersListResponse1](../../doc/models/portfolios-orders-list-response-1.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort23? sort = Sort23.Id;
Order58? order = Order58.Asc;
int? limit = 100;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
try
{
    ApiResponse<PortfoliosOrdersListResponse1> result = await liquidationsApi.ListAccountsLiquidationsAsync(
        accountId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit,
        startDate,
        endDate
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
    "offset": 0,
    "limit": 100,
    "count": 1,
    "total_count": 1,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "c5869b0c-e107-4e68-a89a-57ad43253dd6",
      "created_at": "2021-07-21T14:10:00.000Z",
      "updated_at": "2021-07-21T14:10:00.000Z",
      "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
      "cash_amount": "1000",
      "currency": "EUR",
      "status": "PROCESSING",
      "orders": [
        {
          "id": "b291d8a4-21f5-4c52-8590-4f90ce54d0b1",
          "side": "SELL",
          "status": "NEW"
        },
        {
          "id": "81d02bd9-7b9d-443d-bbfa-31df4d543f7d",
          "side": "SELL",
          "status": "PROCESSING"
        },
        {
          "id": "e97fb564-ebb4-4f37-930a-bda49c09a2e4",
          "side": "SELL",
          "status": "FILLED"
        }
      ],
      "fee_collection_id": null
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Account Liquidation

Initiates a full liquidation of an investment account, placing sell orders for all held positions and converting the proceeds to cash.

Exactly one of `user_id` or `business_id` must be provided in the request body.

See the Liquidations guide ([TOL](https://docs.upvest.co/products/tol/guides/liquidations) / [BYOL](https://docs.upvest.co/products/byol/guides/liquidations)) for implementation details.

```csharp
CreateAccountLiquidationAsync(
    Guid accountId,
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateAccountLiquidationBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateAccountLiquidationBody`](../../doc/models/containers/create-account-liquidation-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`account_liquidations:admin`

## Response Type

**202**: Account liquidation object

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountLiquidation](../../doc/models/account-liquidation.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
CreateAccountLiquidationBody body = CreateAccountLiquidationBody.FromAccountLiquidationRequest(
    new AccountLiquidationRequest
    {
        UserId = new Guid("084a00a8-4a37-4d2d-b276-71ec5e2be0e3"),
    }
);

try
{
    ApiResponse<AccountLiquidation> result = await liquidationsApi.CreateAccountLiquidationAsync(
        accountId,
        upvestClientId,
        idempotencyKey,
        upvestApiVersion,
        body
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
  "id": "b2f541dc-a27b-4d5a-b970-41812b0892b9",
  "created_at": "2021-07-21T14:10:00.000Z",
  "updated_at": "2021-07-21T14:10:00.000Z",
  "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
  "user_id": "81dfb00e-9be6-4617-8994-dfd0407f34df",
  "cash_amount": "1000",
  "currency": "EUR",
  "status": "NEW",
  "orders": [],
  "fee_collection_id": null
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Account Liquidation

Retrieves an account liquidation by its ID, including the status of each individual sell order.

See the Liquidations guide ([TOL](https://docs.upvest.co/products/tol/guides/liquidations) / [BYOL](https://docs.upvest.co/products/byol/guides/liquidations)) for implementation details.

```csharp
RetrieveAccountLiquidationAsync(
    Guid accountId,
    Guid accountLiquidationId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `accountLiquidationId` | `Guid` | Template, Required | The unique identifier of the account liquidation. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`account_liquidations:admin`, `account_liquidations:read`

## Response Type

**200**: Account liquidation object

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountLiquidation](../../doc/models/account-liquidation.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid accountLiquidationId = new Guid("0000152a-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AccountLiquidation> result = await liquidationsApi.RetrieveAccountLiquidationAsync(
        accountId,
        accountLiquidationId,
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
  "id": "f386fdc4-dcd8-4dce-8acb-dae332f51483",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "4b9732bd-7496-4e91-8a5f-6360479d7fed",
  "account_id": "f2f7d2c7-d79b-4de2-ae9b-d01646259f9e",
  "cash_amount": "100.00",
  "currency": "EUR",
  "status": "PROCESSING",
  "orders": [
    {
      "id": "79f2c816-13ed-4588-85e6-f3398b0b5825",
      "side": "SELL",
      "status": "NEW"
    },
    {
      "id": "44f50a4e-7ce8-4eae-a078-f4b3d8ab4708",
      "side": "SELL",
      "status": "NEW"
    }
  ],
  "fee_collection_id": "8a6a2b7c-3f4e-4d8a-9c1b-1234567890ab"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Cancel Account Liquidation

Cancels a pending account liquidation. Only liquidations in the `NEW` status can be cancelled.

See the Liquidations guide ([TOL](https://docs.upvest.co/products/tol/guides/liquidations) / [BYOL](https://docs.upvest.co/products/byol/guides/liquidations)) for implementation details.

```csharp
CancelAccountLiquidationAsync(
    Guid accountId,
    Guid accountLiquidationId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `accountLiquidationId` | `Guid` | Template, Required | The unique identifier of the account liquidation. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`account_liquidations:admin`

## Response Type

**204**: Request has been processed successfully.

`Task`

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid accountLiquidationId = new Guid("0000152a-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await liquidationsApi.CancelAccountLiquidationAsync(
        accountId,
        accountLiquidationId,
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

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

