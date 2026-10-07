# Orders

All order related paths.

```csharp
OrdersApi ordersApi = client.OrdersApi;
```

## Class Name

`OrdersApi`

## Methods

* [List Account Orders](../../doc/controllers/orders.md#list-account-orders)
* [Place Order](../../doc/controllers/orders.md#place-order)
* [Retrieve Order](../../doc/controllers/orders.md#retrieve-order)
* [Cancel Order](../../doc/controllers/orders.md#cancel-order)
* [Retrieve Order Execution](../../doc/controllers/orders.md#retrieve-order-execution)


# List Account Orders

Returns a paginated list of orders for the account specified by its ID, including their statuses, executions, and fee configurations.

See the Orders guide ([TOL](https://docs.upvest.co/products/tol/guides/orders) / [BYOL](https://docs.upvest.co/products/byol/guides/orders) / [Omnibus](https://docs.upvest.co/products/omnibus/guides/orders)) for order lifecycle details.

```csharp
ListAccountOrdersAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Order? order = Models.Order.Asc,
    Guid? accountGroupId = null,
    Guid? userId = null,
    Models.Status50? status = Models.Status50.Filled,
    DateTime? dateCreatedFrom = null,
    DateTime? dateCreatedTo = null,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `accountGroupId` | `Guid?` | Query, Optional | Filters results by account group ID. Universally Unique Identifier (UUID). |
| `userId` | `Guid?` | Query, Optional | Filters results by user ID. Universally Unique Identifier (UUID). |
| `status` | [`Status50?`](../../doc/models/status-50.md) | Query, Optional | The execution status of the order.<br><br>* NEW -<br>* PROCESSING -<br>* FILLED -<br>* CANCELLED -<br><br>**Default**: `Status50.FILLED` |
| `dateCreatedFrom` | `DateTime?` | Query, Optional | Filters results to those created at or after this timestamp. [RFC 3339](https://datatracker.ietf.org/doc/html/rfc3339) date-time format (e.g. `2024-01-01`). |
| `dateCreatedTo` | `DateTime?` | Query, Optional | Filters results to those created at or before this timestamp. [RFC 3339](https://datatracker.ietf.org/doc/html/rfc3339) date-time format (e.g. `2024-12-31T23:59:59Z`). |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`orders:admin`, `orders:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.OrdersListResponse](../../doc/models/orders-list-response.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Order? order = Order.Asc;
Status50? status = Status50.Filled;
DateTime? dateCreatedFrom = DateTime.ParseExact("10/14/2022 10:10:10", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);
DateTime? dateCreatedTo = DateTime.ParseExact("10/14/2022 10:10:10", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind);
int? limit = 100;
try
{
    ApiResponse<OrdersListResponse> result = await ordersApi.ListAccountOrdersAsync(
        accountId,
        upvestClientId,
        upvestApiVersion,
        order,
        null,
        null,
        status,
        dateCreatedFrom,
        dateCreatedTo,
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
    "total_count": 1,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "user_id": "2dedfeb0-58cd-44f2-ae08-0e41fe0413d9",
      "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
      "cash_amount": "56.65",
      "currency": "EUR",
      "side": "BUY",
      "instrument_id": "US0378331005",
      "instrument_id_type": "ISIN",
      "order_type": "MARKET",
      "quantity": "0.05",
      "user_instrument_fit_acknowledgement": true,
      "limit_price": "",
      "stop_price": "",
      "status": "FILLED",
      "fee": "0.5",
      "executions": [
        {
          "id": "b9dc0676-8a7d-412d-802a-3b325eefd15e",
          "side": "BUY",
          "currency": "EUR",
          "status": "FILLED",
          "order_id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
          "cash_amount": "56.65",
          "share_quantity": "0.05",
          "price": "130.65",
          "transaction_time": "2021-07-21T14:10:00.00Z",
          "taxes": [
            {
              "amount": "1.3",
              "type": "TOTAL"
            }
          ],
          "venue_id": "20d6024b-2df4-41ae-8d42-62e4744e455b"
        }
      ],
      "client_reference": "",
      "initiation_flow": "API"
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


# Place Order

Places a new order for an instrument. After the request is accepted, the order is processed asynchronously — use the returned `id` to poll for status or subscribe to order webhook events.

See the Orders guide ([TOL](https://docs.upvest.co/products/tol/guides/orders) / [BYOL](https://docs.upvest.co/products/byol/guides/orders) / [Omnibus](https://docs.upvest.co/products/omnibus/guides/orders)) for order lifecycle details.

```csharp
PlaceOrderAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.OrderPlaceRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`OrderPlaceRequest`](../../doc/models/order-place-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`orders:admin`

## Response Type

**202**: The request for the order creation has been accepted for processing.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Order39](../../doc/models/order-39.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
OrderPlaceRequest body = new OrderPlaceRequest
{
    AccountId = new Guid("debf2026-f2da-4ff0-bb84-92e45babb1e3"),
    Side = Side.Buy,
    InstrumentId = "US0378331005",
    InstrumentIdType = "ISIN",
    UserId = new Guid("2dedfeb0-58cd-44f2-ae08-0e41fe0413d9"),
    CashAmount = "1000",
    Currency = Currency29.Eur,
    OrderType = OrderType.Market,
    UserInstrumentFitAcknowledgement = true,
    LimitPrice = "",
    StopPrice = "",
};

try
{
    ApiResponse<Order39> result = await ordersApi.PlaceOrderAsync(
        upvestClientId,
        idempotencyKey,
        null,
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
  "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "2dedfeb0-58cd-44f2-ae08-0e41fe0413d9",
  "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
  "cash_amount": "1000",
  "currency": "EUR",
  "side": "BUY",
  "instrument_id": "US0378331005",
  "instrument_id_type": "ISIN",
  "order_type": "MARKET",
  "quantity": "0",
  "user_instrument_fit_acknowledgement": true,
  "limit_price": "",
  "stop_price": "",
  "status": "NEW",
  "fee": "0.0",
  "executions": [],
  "client_reference": "",
  "initiation_flow": "API"
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 400 | Bad Request. The incoming request had a malformed parameter/object. | [`ErrorException`](../../doc/models/error-exception.md) |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 422 | Unprocessable Entity. The syntax of request is correct but server can't process it due a semantic error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Order

Returns the order specified by its ID, including its current status, executions, fee configuration, and any cancellation reason.

See the Orders guide ([TOL](https://docs.upvest.co/products/tol/guides/orders) / [BYOL](https://docs.upvest.co/products/byol/guides/orders) / [Omnibus](https://docs.upvest.co/products/omnibus/guides/orders)) for order lifecycle details.

```csharp
RetrieveOrderAsync(
    Guid orderId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `orderId` | `Guid` | Template, Required | The unique identifier of the order. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`orders:admin`, `orders:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Order39](../../doc/models/order-39.md).

## Example Usage

```csharp
Guid orderId = new Guid("00001a1e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<Order39> result = await ordersApi.RetrieveOrderAsync(
        orderId,
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
  "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "2dedfeb0-58cd-44f2-ae08-0e41fe0413d9",
  "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
  "cash_amount": "56.65",
  "currency": "EUR",
  "side": "BUY",
  "instrument_id": "US0378331005",
  "instrument_id_type": "ISIN",
  "order_type": "MARKET",
  "quantity": "0.05",
  "user_instrument_fit_acknowledgement": true,
  "limit_price": "",
  "stop_price": "",
  "status": "NEW",
  "fee": "0.5",
  "executions": [],
  "client_reference": "",
  "initiation_flow": "API",
  "execution_flow": "STRAIGHT_THROUGH"
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


# Cancel Order

Cancels an order specified by its ID. It is possible to cancel an order in the `NEW` or `PROCESSING` status. Once a cancellation has been accepted, the further processing steps take place asynchronously and depending on the respective order status.

```csharp
CancelOrderAsync(
    Guid orderId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `orderId` | `Guid` | Template, Required | The unique identifier of the order. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`orders:admin`

## Response Type

**202**: The order cancelling request is accepted.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.OrderCancelResponse](../../doc/models/order-cancel-response.md).

## Example Usage

```csharp
Guid orderId = new Guid("00001a1e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<OrderCancelResponse> result = await ordersApi.CancelOrderAsync(
        orderId,
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
  "id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c"
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


# Retrieve Order Execution

Returns the order execution specified by its ID. An execution represents a single trade fill within an order; an order may have multiple executions if it is partially filled across several trades.

See the Orders guide ([TOL](https://docs.upvest.co/products/tol/guides/orders) / [BYOL](https://docs.upvest.co/products/byol/guides/orders) / [Omnibus](https://docs.upvest.co/products/omnibus/guides/orders)) for execution lifecycle details.

```csharp
RetrieveOrderExecutionAsync(
    Guid executionId,
    Guid orderId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `executionId` | `Guid` | Template, Required | The unique identifier of the order execution. Universally Unique Identifier (UUID). |
| `orderId` | `Guid` | Template, Required | The unique identifier of the order. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`orders:admin`, `orders:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.OrderExecution](../../doc/models/order-execution.md).

## Example Usage

```csharp
Guid executionId = new Guid("000009be-0000-0000-0000-000000000000");
Guid orderId = new Guid("00001a1e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<OrderExecution> result = await ordersApi.RetrieveOrderExecutionAsync(
        executionId,
        orderId,
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
  "id": "b9dc0676-8a7d-412d-802a-3b325eefd15e",
  "side": "BUY",
  "currency": "EUR",
  "status": "SETTLED",
  "order_id": "eb5ba93f-5dfe-4bf1-8571-4da0caacc80c",
  "cash_amount": "56.65",
  "share_quantity": "0.05",
  "price": "130.65",
  "transaction_time": "2021-07-21T14:10:00.00Z",
  "taxes": [
    {
      "amount": "1.3",
      "type": "TOTAL"
    }
  ],
  "settlement_date": "2021-07-23",
  "venue_id": "20d6024b-2df4-41ae-8d42-62e4744e455b"
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

