# Portfolios Rebalancing

```csharp
PortfoliosRebalancingApi portfoliosRebalancingApi = client.PortfoliosRebalancingApi;
```

## Class Name

`PortfoliosRebalancingApi`

## Methods

* [Trigger Portfolio Rebalancing](../../doc/controllers/portfolios-rebalancing.md#trigger-portfolio-rebalancing)
* [Retrieve Portfolios Rebalancing Execution](../../doc/controllers/portfolios-rebalancing.md#retrieve-portfolios-rebalancing-execution)
* [List Portfolio Rebalancing Execution Orders](../../doc/controllers/portfolios-rebalancing.md#list-portfolio-rebalancing-execution-orders)


# Trigger Portfolio Rebalancing

Triggers an immediate rebalancing execution for all accounts linked to the specified allocation.

The rebalancing adjusts each account's positions to match the current target allocation weights. In rare situations, individual orders within the rebalancing may not be cancellable once submitted.

See the Portfolios rebalancing guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios/rebalancing/rebalancing) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios/rebalancing/rebalancing)) for implementation details.

```csharp
TriggerPortfolioRebalancingAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    TriggerPortfolioRebalancingBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TriggerPortfolioRebalancingBody`](../../doc/models/containers/trigger-portfolio-rebalancing-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolio

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.TriggerPortfolioRebalancingResponse](../../doc/models/trigger-portfolio-rebalancing-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
TriggerPortfolioRebalancingBody body = TriggerPortfolioRebalancingBody.FromAccounts(
    new Accounts
    {
        Accounts = new List<Guid>
        {
            new Guid("43801b85-bbcf-41c7-8389-b018082a5ec5"),
            new Guid("20151679-c159-4f16-b8a2-267b3d968c6f"),
        },
    }
);

try
{
    ApiResponse<TriggerPortfolioRebalancingResponse> result = await portfoliosRebalancingApi.TriggerPortfolioRebalancingAsync(
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
  "id": "735381ce-8380-4b07-9e42-97e2b6d3643e"
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


# Retrieve Portfolios Rebalancing Execution

Retrieve portfolios rebalancing execution

```csharp
RetrievePortfoliosRebalancingExecutionAsync(
    Guid executionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `executionId` | `Guid` | Template, Required | The unique identifier of the order execution. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosRebalancingExecution](../../doc/models/portfolios-rebalancing-execution.md).

## Example Usage

```csharp
Guid executionId = new Guid("000009be-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PortfoliosRebalancingExecution> result = await portfoliosRebalancingApi.RetrievePortfoliosRebalancingExecutionAsync(
        executionId,
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
  "id": "735381ce-8380-4b07-9e42-97e2b6d3643e",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "accounts": [
    "43801b85-bbcf-41c7-8389-b018082a5ec5",
    "20151679-c159-4f16-b8a2-267b3d968c6f"
  ],
  "allocations": []
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 405 | Method Not Allowed. The requested method is not allowed on the requested resource. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Portfolio Rebalancing Execution Orders

List portfolio rebalancing execution orders

```csharp
ListPortfolioRebalancingExecutionOrdersAsync(
    Guid executionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort26? sort = Models.Sort26.Status,
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
| `executionId` | `Guid` | Template, Required | The unique identifier of the order execution. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort26?`](../../doc/models/sort-26.md) | Query, Optional | Sort the result by `status`.<br><br>**Default**: `Sort26.status` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `startDate` | `string` | Query, Optional | Returns rebalancing orders with dates starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns rebalancing orders with dates up until this date (UTC) |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosRebalancingExecutionOrderListResponse](../../doc/models/portfolios-rebalancing-execution-order-list-response.md).

## Example Usage

```csharp
Guid executionId = new Guid("000009be-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort26? sort = Sort26.Status;
Order58? order = Order58.Asc;
int? limit = 100;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
try
{
    ApiResponse<PortfoliosRebalancingExecutionOrderListResponse> result = await portfoliosRebalancingApi.ListPortfolioRebalancingExecutionOrdersAsync(
        executionId,
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
    "count": 2,
    "limit": 10,
    "offset": 0,
    "order": "ASC",
    "sort": "state",
    "total_count": 2
  },
  "data": [
    {
      "id": "adf706aa-4fac-4e1d-878b-5b225f7b7354",
      "created_at": "2022-09-28T09:14:02Z",
      "updated_at": "2022-09-28T09:14:02Z",
      "execution_id": "edc74b48-a377-4b93-84fb-543b1137de05",
      "account_id": "100bb408-149c-4e6f-8697-837c2a144a29",
      "portfolio_order_id": "b31723e0-8c8c-4bd0-a962-8bd26c5aee4d",
      "status": "FILLED"
    },
    {
      "id": "1dd8cede-a326-43b3-a54c-9d2d0e80edb5",
      "created_at": "2022-09-28T09:14:02Z",
      "updated_at": "2022-09-28T09:14:02Z",
      "execution_id": "f678fe2b-ffd8-4b99-844f-a8f78a67e528",
      "account_id": "98b21a45-5ea0-481f-bb1e-880aab0d154d",
      "portfolio_order_id": "411c625e-b2f4-4af4-ac21-e17846fb6bcc",
      "status": "PROCESSING"
    },
    {
      "id": "1bf46252-ad90-4e22-8717-9586d2b86327",
      "created_at": "2022-09-28T09:14:02Z",
      "updated_at": "2022-09-28T09:14:02Z",
      "execution_id": "9275e08d-012f-40bd-80b7-58037bdc7ab6",
      "portfolio_order_id": null,
      "account_id": "98b21a45-5ea0-481f-bb1e-880aab0d154d",
      "status": "NEW"
    },
    {
      "id": "1bf46252-ad90-4e22-8717-9586d2b86327",
      "created_at": "2022-09-28T09:14:02Z",
      "updated_at": "2022-09-28T09:14:02Z",
      "execution_id": "9275e08d-012f-40bd-80b7-58037bdc7ab6",
      "portfolio_order_id": null,
      "account_id": "98b21a45-5ea0-481f-bb1e-880aab0d154d",
      "status": "CANCELLED",
      "cancellation_reason": "PORTFOLIO_IS_BALANCED",
      "cancellation_details": "Portfolio is balanced, no need to rebalance."
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

