# Virtual Cash Balances

```csharp
VirtualCashBalancesApi virtualCashBalancesApi = client.VirtualCashBalancesApi;
```

## Class Name

`VirtualCashBalancesApi`

## Methods

* [Create Virtual Cash Increase](../../doc/controllers/virtual-cash-balances.md#create-virtual-cash-increase)
* [Create Virtual Cash Decrease](../../doc/controllers/virtual-cash-balances.md#create-virtual-cash-decrease)
* [Cancel Virtual Cash Decrease](../../doc/controllers/virtual-cash-balances.md#cancel-virtual-cash-decrease)


# Create Virtual Cash Increase

Trigger a virtual cash increase

```csharp
CreateVirtualCashIncreaseAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.VirtualCashBalanceVirtualCashIncreaseCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`VirtualCashBalanceVirtualCashIncreaseCreateRequest`](../../doc/models/virtual-cash-balance-virtual-cash-increase-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`virtual_cash_balances:admin`

## Response Type

**202**: Virtual Cash Balances Increase

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.VirtualCashBalanceVirtualCashIncrease](../../doc/models/virtual-cash-balance-virtual-cash-increase.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
VirtualCashBalanceVirtualCashIncreaseCreateRequest body = new VirtualCashBalanceVirtualCashIncreaseCreateRequest
{
    AccountGroupId = new Guid("2596db3b-0d03-4651-9eda-970910479dfb"),
    Amount = "200.00",
    Currency = Currency1.Eur,
};

try
{
    ApiResponse<VirtualCashBalanceVirtualCashIncrease> result = await virtualCashBalancesApi.CreateVirtualCashIncreaseAsync(
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
  "id": "6ffa6b16-2380-4e7a-88b2-ae625c8eef99",
  "created_at": "2020-08-24T14:15:22Z",
  "updated_at": "2020-08-24T14:15:22Z",
  "account_group_id": "ac1c39e9-2101-46b8-a624-d10a9e351b6c",
  "amount": "200.00",
  "currency": "EUR",
  "status": "ISSUED"
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


# Create Virtual Cash Decrease

Trigger a virtual cash decrease

```csharp
CreateVirtualCashDecreaseAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.VirtualCashBalanceVirtualCashDecreaseCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`VirtualCashBalanceVirtualCashDecreaseCreateRequest`](../../doc/models/virtual-cash-balance-virtual-cash-decrease-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`virtual_cash_balances:admin`

## Response Type

**202**: Virtual Cash Balances Decrease

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.VirtualCashBalanceVirtualCashDecrease](../../doc/models/virtual-cash-balance-virtual-cash-decrease.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
VirtualCashBalanceVirtualCashDecreaseCreateRequest body = new VirtualCashBalanceVirtualCashDecreaseCreateRequest
{
    AccountGroupId = new Guid("2596db3b-0d03-4651-9eda-970910479dfb"),
    Amount = "200.00",
    Currency = Currency1.Eur,
};

try
{
    ApiResponse<VirtualCashBalanceVirtualCashDecrease> result = await virtualCashBalancesApi.CreateVirtualCashDecreaseAsync(
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
  "id": "6ffa6b16-2380-4e7a-88b2-ae625c8eef99",
  "created_at": "2020-08-24T14:15:22Z",
  "updated_at": "2020-08-24T14:15:22Z",
  "account_group_id": "ac1c39e9-2101-46b8-a624-d10a9e351b6c",
  "amount": "200.00",
  "currency": "EUR",
  "status": "ISSUED"
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


# Cancel Virtual Cash Decrease

Cancels a virtual cash decrease specified by its ID. It is only possible to cancel a virtual cash decrease if it has the status `ISSUED` or `QUEUED`.

```csharp
CancelVirtualCashDecreaseAsync(
    Guid virtualCashDecreaseId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `virtualCashDecreaseId` | `Guid` | Template, Required | The unique identifier of the virtual cash decrease. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`virtual_cash_balances:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid virtualCashDecreaseId = new Guid("000010ec-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await virtualCashBalancesApi.CancelVirtualCashDecreaseAsync(
        virtualCashDecreaseId,
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
| 422 | Unprocessable Entity. The syntax of request is correct but server can't process it due a semantic error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

