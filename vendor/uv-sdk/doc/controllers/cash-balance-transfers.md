# Cash Balance Transfers

```csharp
CashBalanceTransfersApi cashBalanceTransfersApi = client.CashBalanceTransfersApi;
```

## Class Name

`CashBalanceTransfersApi`

## Methods

* [List Cash Balance Transfers](../../doc/controllers/cash-balance-transfers.md#list-cash-balance-transfers)
* [Create Cash Balance Transfer](../../doc/controllers/cash-balance-transfers.md#create-cash-balance-transfer)
* [Retrieve Cash Balance Transfer](../../doc/controllers/cash-balance-transfers.md#retrieve-cash-balance-transfer)
* [Cancel Cash Balance Transfer](../../doc/controllers/cash-balance-transfers.md#cancel-cash-balance-transfer)
* [List User Cash Balance Transfers](../../doc/controllers/cash-balance-transfers.md#list-user-cash-balance-transfers)
* [List Business Cash Balance Transfers](../../doc/controllers/cash-balance-transfers.md#list-business-cash-balance-transfers)


# List Cash Balance Transfers

Returns a paginated list of all cash balance transfers initiated by the client, across all account groups and users.

```csharp
ListCashBalanceTransfersAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`, `cash_balance_transfers:read`

## Response Type

**200**: Cash balance transfers list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.CashBalanceTransfersListResponse](../../doc/models/cash-balance-transfers-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<CashBalanceTransfersListResponse> result = await cashBalanceTransfersApi.ListCashBalanceTransfersAsync(
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit
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
    "total_count": 1
  },
  "data": [
    {
      "id": "b3f6a0c2-1d7e-4a3b-9f21-0c2d4e6f8a10",
      "created_at": "2026-06-25T14:15:22Z",
      "updated_at": "2026-06-25T14:15:23Z",
      "source_account_group_id": "2596db3b-0d03-4651-9eda-970910479dfb",
      "target_account_group_id": "e56c14b3-8370-4c04-bc67-7092f7ec113c",
      "amount": "200.00",
      "currency": "EUR",
      "status": "CONFIRMED",
      "cancellation_reason": null
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Cash Balance Transfer

Create an internal cash balance transfer between two account groups belonging to the same user and tenant. The cash leaves the source account group and arrives at the target account group as a single operation, with no external bank transfer. Only settled cash denominated in the same currency (EUR) can be transferred. Both account groups must be of the same type, and the type must be one of `PERSONAL`, `CHILD`, or `BUSINESS`.

```csharp
CreateCashBalanceTransferAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PaymentsCashBalanceTransferCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PaymentsCashBalanceTransferCreateRequest`](../../doc/models/payments-cash-balance-transfer-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`

## Response Type

**200**: Cash balance transfer

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsCashBalanceTransfer](../../doc/models/payments-cash-balance-transfer.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PaymentsCashBalanceTransferCreateRequest body = new PaymentsCashBalanceTransferCreateRequest
{
    SourceAccountGroupId = new Guid("2596db3b-0d03-4651-9eda-970910479dfb"),
    TargetAccountGroupId = new Guid("e56c14b3-8370-4c04-bc67-7092f7ec113c"),
    Amount = "200.00",
    Currency = Currency.Eur,
};

try
{
    ApiResponse<PaymentsCashBalanceTransfer> result = await cashBalanceTransfersApi.CreateCashBalanceTransferAsync(
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
  "id": "b3f6a0c2-1d7e-4a3b-9f21-0c2d4e6f8a10",
  "created_at": "2026-06-25T14:15:22Z",
  "updated_at": "2026-06-25T14:15:22Z",
  "source_account_group_id": "2596db3b-0d03-4651-9eda-970910479dfb",
  "target_account_group_id": "e56c14b3-8370-4c04-bc67-7092f7ec113c",
  "amount": "200.00",
  "currency": "EUR",
  "status": "ISSUED",
  "cancellation_reason": null
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


# Retrieve Cash Balance Transfer

Retrieve a cash balance transfer by its ID.

```csharp
RetrieveCashBalanceTransferAsync(
    Guid cashBalanceTransferId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `cashBalanceTransferId` | `Guid` | Template, Required | The unique identifier of the cash balance transfer. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`, `cash_balance_transfers:read`

## Response Type

**200**: Cash balance transfer

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PaymentsCashBalanceTransfer](../../doc/models/payments-cash-balance-transfer.md).

## Example Usage

```csharp
Guid cashBalanceTransferId = new Guid("000014ee-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PaymentsCashBalanceTransfer> result = await cashBalanceTransfersApi.RetrieveCashBalanceTransferAsync(
        cashBalanceTransferId,
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
  "id": "b3f6a0c2-1d7e-4a3b-9f21-0c2d4e6f8a10",
  "created_at": "2026-06-25T14:15:22Z",
  "updated_at": "2026-06-25T14:15:23Z",
  "source_account_group_id": "2596db3b-0d03-4651-9eda-970910479dfb",
  "target_account_group_id": "e56c14b3-8370-4c04-bc67-7092f7ec113c",
  "amount": "200.00",
  "currency": "EUR",
  "status": "CONFIRMED",
  "cancellation_reason": null
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


# Cancel Cash Balance Transfer

Cancels a cash balance transfer specified by its ID. It is only possible to cancel a transfer while it has the status `ISSUED`.

```csharp
CancelCashBalanceTransferAsync(
    Guid cashBalanceTransferId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `cashBalanceTransferId` | `Guid` | Template, Required | The unique identifier of the cash balance transfer. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`

## Response Type

**204**: Request has been processed successfully.

`Task`

## Example Usage

```csharp
Guid cashBalanceTransferId = new Guid("000014ee-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await cashBalanceTransfersApi.CancelCashBalanceTransferAsync(
        cashBalanceTransferId,
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
| 422 | Unprocessable Entity. The syntax of request is correct but server can't process it due a semantic error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List User Cash Balance Transfers

Returns the list of cash balance transfers of a user specified by ID.

```csharp
ListUserCashBalanceTransfersAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`, `cash_balance_transfers:read`

## Response Type

**200**: Cash balance transfers list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.UserCashBalanceTransfersListResponse](../../doc/models/user-cash-balance-transfers-list-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<UserCashBalanceTransfersListResponse> result = await cashBalanceTransfersApi.ListUserCashBalanceTransfersAsync(
        userId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit
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
    "total_count": 1
  },
  "data": [
    {
      "id": "b3f6a0c2-1d7e-4a3b-9f21-0c2d4e6f8a10",
      "created_at": "2026-06-25T14:15:22Z",
      "updated_at": "2026-06-25T14:15:23Z",
      "source_account_group_id": "2596db3b-0d03-4651-9eda-970910479dfb",
      "target_account_group_id": "e56c14b3-8370-4c04-bc67-7092f7ec113c",
      "amount": "200.00",
      "currency": "EUR",
      "status": "CONFIRMED",
      "cancellation_reason": null
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Business Cash Balance Transfers

Returns the list of cash balance transfers of a business specified by ID.

```csharp
ListBusinessCashBalanceTransfersAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`cash_balance_transfers:admin`, `cash_balance_transfers:read`

## Response Type

**200**: Cash balance transfers list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.BusinessCashBalanceTransfersListResponse](../../doc/models/business-cash-balance-transfers-list-response.md).

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<BusinessCashBalanceTransfersListResponse> result = await cashBalanceTransfersApi.ListBusinessCashBalanceTransfersAsync(
        businessId,
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit
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
    "total_count": 1
  },
  "data": [
    {
      "id": "b3f6a0c2-1d7e-4a3b-9f21-0c2d4e6f8a10",
      "created_at": "2026-06-25T14:15:22Z",
      "updated_at": "2026-06-25T14:15:23Z",
      "source_account_group_id": "2596db3b-0d03-4651-9eda-970910479dfb",
      "target_account_group_id": "e56c14b3-8370-4c04-bc67-7092f7ec113c",
      "amount": "200.00",
      "currency": "EUR",
      "status": "CONFIRMED",
      "cancellation_reason": null
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |

