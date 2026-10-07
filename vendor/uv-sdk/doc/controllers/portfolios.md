# Portfolios

All portfolios related paths.

```csharp
PortfoliosApi portfoliosApi = client.PortfoliosApi;
```

## Class Name

`PortfoliosApi`

## Methods

* [List Portfolios Allocations](../../doc/controllers/portfolios.md#list-portfolios-allocations)
* [Create Portfolios Allocation](../../doc/controllers/portfolios.md#create-portfolios-allocation)
* [Retrieve Portfolios Allocation](../../doc/controllers/portfolios.md#retrieve-portfolios-allocation)
* [Update Portfolios Allocation](../../doc/controllers/portfolios.md#update-portfolios-allocation)
* [List Portfolios Configurations](../../doc/controllers/portfolios.md#list-portfolios-configurations)
* [Create Portfolios Configuration](../../doc/controllers/portfolios.md#create-portfolios-configuration)
* [Retrieve Portfolios Configuration](../../doc/controllers/portfolios.md#retrieve-portfolios-configuration)
* [Update Portfolios Configuration](../../doc/controllers/portfolios.md#update-portfolios-configuration)
* [Delete Portfolios Configuration](../../doc/controllers/portfolios.md#delete-portfolios-configuration)
* [List Portfolios Orders](../../doc/controllers/portfolios.md#list-portfolios-orders)
* [Create Portfolios Order](../../doc/controllers/portfolios.md#create-portfolios-order)
* [Retrieve Portfolios Order](../../doc/controllers/portfolios.md#retrieve-portfolios-order)
* [Cancel Portfolios Order](../../doc/controllers/portfolios.md#cancel-portfolios-order)
* [List Portfolios Allocation Accounts](../../doc/controllers/portfolios.md#list-portfolios-allocation-accounts)


# List Portfolios Allocations

Returns a paginated list of portfolio allocations for the authenticated client.

Allocations define the target instrument weights that portfolio configurations use to invest and rebalance.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
ListPortfoliosAllocationsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort23? sort = Models.Sort23.Id,
    Models.Order58? order = Models.Order58.Asc,
    int? offset = null,
    int? limit = 100,
    string startDate = null,
    string endDate = null,
    string instrumentIds = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort23?`](../../doc/models/sort-23.md) | Query, Optional | Sort the result by `id`.<br><br>**Default**: `Sort23.id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `startDate` | `string` | Query, Optional | Returns portfolio allocations with dates starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns portfolio allocations with dates up until this date (UTC) |
| `instrumentIds` | `string` | Query, Optional | Filters portfolio allocations containing the instruments ID's |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios allocations

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosAllocationsListResponse](../../doc/models/portfolios-allocations-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort23? sort = Sort23.Id;
Order58? order = Order58.Asc;
int? limit = 100;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
string instrumentIds = "bf72bdc1-b342-494f-9739-b27ae9011b86,dcbe2cf4-4f37-4fe5-9393-4fb52bb87fab";
try
{
    ApiResponse<PortfoliosAllocationsListResponse> result = await portfoliosApi.ListPortfoliosAllocationsAsync(
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit,
        startDate,
        endDate,
        instrumentIds
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


# Create Portfolios Allocation

Creates a named portfolio allocation specifying a set of instrument weights.

The `allocation` array must contain at least one instrument entry. Weights are expressed as percentages and must sum to 100.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
CreatePortfoliosAllocationAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PortfoliosAllocationCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PortfoliosAllocationCreateRequest`](../../doc/models/portfolios-allocation-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolios allocation

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosAllocation](../../doc/models/portfolios-allocation.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PortfoliosAllocationCreateRequest body = new PortfoliosAllocationCreateRequest
{
    Allocation = new List<Allocation>
    {
        new Allocation
        {
            InstrumentId = AllocationInstrumentId.FromString("String3"),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = "weight6",
        },
    },
};

try
{
    ApiResponse<PortfoliosAllocation> result = await portfoliosApi.CreatePortfoliosAllocationAsync(
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


# Retrieve Portfolios Allocation

Retrieves a portfolio allocation by its ID, including its name, instrument weights, and associated rebalancing strategy IDs.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
RetrievePortfoliosAllocationAsync(
    Guid allocationId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `allocationId` | `Guid` | Template, Required | The unique identifier of the portfolio allocation. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios allocation

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosAllocation](../../doc/models/portfolios-allocation.md).

## Example Usage

```csharp
Guid allocationId = new Guid("00001ad8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PortfoliosAllocation> result = await portfoliosApi.RetrievePortfoliosAllocationAsync(
        allocationId,
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


# Update Portfolios Allocation

Updates the name and/or instrument weights of an existing portfolio allocation.

Accounts with a portfolio configuration referencing this allocation will use the new weights on their next rebalancing.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
UpdatePortfoliosAllocationAsync(
    Guid allocationId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PortfoliosAllocationUpdateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `allocationId` | `Guid` | Template, Required | The unique identifier of the portfolio allocation. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PortfoliosAllocationUpdateRequest`](../../doc/models/portfolios-allocation-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolios allocation

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosAllocation](../../doc/models/portfolios-allocation.md).

## Example Usage

```csharp
Guid allocationId = new Guid("00001ad8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PortfoliosAllocationUpdateRequest body = new PortfoliosAllocationUpdateRequest
{
    Allocation = new List<Allocation>
    {
        new Allocation
        {
            InstrumentId = AllocationInstrumentId.FromString("String3"),
            InstrumentIdType = InstrumentIdType4.Isin,
            Weight = "weight6",
        },
    },
};

try
{
    ApiResponse<PortfoliosAllocation> result = await portfoliosApi.UpdatePortfoliosAllocationAsync(
        allocationId,
        upvestClientId,
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


# List Portfolios Configurations

Returns a paginated list of portfolio configurations for the authenticated client.

Each configuration links an account to an allocation and defines which rebalancing strategies apply to that account.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
ListPortfoliosConfigurationsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort24? sort = Models.Sort24.AccountId,
    Models.Order58? order = Models.Order58.Asc,
    int? offset = null,
    int? limit = 100,
    string startDate = null,
    string endDate = null,
    string accountId = null,
    string allocationId = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort24?`](../../doc/models/sort-24.md) | Query, Optional | Sort the result by `account_id`.<br><br>**Default**: `Sort24.account_id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `startDate` | `string` | Query, Optional | Returns portfolio configurations with dates starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns portfolio configurations with dates up until this date (UTC) |
| `accountId` | `string` | Query, Optional | Filters portfolio configurations by account ID |
| `allocationId` | `string` | Query, Optional | Filters portfolio configurations by allocation ID |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios configurations

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosConfigurationsListResponse](../../doc/models/portfolios-configurations-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort24? sort = Sort24.AccountId;
Order58? order = Order58.Asc;
int? limit = 100;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
string accountId = "3ada8e9e-82c4-4c78-a43d-a691b1636509";
string allocationId = "f6c2b02a-3214-4a40-82ae-6c76df50bc8c";
try
{
    ApiResponse<PortfoliosConfigurationsListResponse> result = await portfoliosApi.ListPortfoliosConfigurationsAsync(
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit,
        startDate,
        endDate,
        accountId,
        allocationId
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
    "sort": "account_id",
    "order": "ASC"
  },
  "data": [
    {
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "account_id": "4ca934ae-66b2-4494-86f9-df6ab89351f1",
      "allocation_id": "65c69f95-c7b9-4c93-9943-a8c7fe4f7aef",
      "rebalancing_strategy_ids": [
        "7a7322f5-b45d-4aba-aae4-c12797938c93",
        "d7b4291b-5a26-40c2-a8dd-235bd904a2ed"
      ]
    },
    {
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "account_id": "a46c4531-ec90-4ac3-ac6b-356d743a3859",
      "allocation_id": "a4117e26-7c31-43ad-af47-0784a4957da9",
      "rebalancing_strategy_ids": [
        "7a7322f5-b45d-4aba-aae4-c12797938c93",
        "692e318a-d6d0-43ac-999a-b84a82dbe304"
      ]
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


# Create Portfolios Configuration

Creates a portfolio configuration for an investment account, associating it with a target allocation and optional rebalancing strategies.

Only one active configuration per account is permitted.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
CreatePortfoliosConfigurationAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PortfoliosConfigurationCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PortfoliosConfigurationCreateRequest`](../../doc/models/portfolios-configuration-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolios configuration

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosConfiguration](../../doc/models/portfolios-configuration.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PortfoliosConfigurationCreateRequest body = new PortfoliosConfigurationCreateRequest
{
    AccountId = new Guid("f2f7d2c7-d79b-4de2-ae9b-d01646259f9e"),
    AllocationId = new Guid("eb68abce-29f4-483f-a384-7f243e77851f"),
};

try
{
    ApiResponse<PortfoliosConfiguration> result = await portfoliosApi.CreatePortfoliosConfigurationAsync(
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
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "account_id": "f2f7d2c7-d79b-4de2-ae9b-d01646259f9e",
  "allocation_id": "eb68abce-29f4-483f-a384-7f243e77851f",
  "rebalancing_strategy_ids": [
    "7a7322f5-b45d-4aba-aae4-c12797938c93",
    "fd8e5363-a89c-4a2f-b3c6-5f857721625f"
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
| 422 | Unprocessable Entity. The syntax of request is correct but server can't process it due a semantic error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Retrieve Portfolios Configuration

Retrieves a portfolio configuration by its ID, including the linked allocation and any associated rebalancing strategies.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
RetrievePortfoliosConfigurationAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosConfiguration](../../doc/models/portfolios-configuration.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PortfoliosConfiguration> result = await portfoliosApi.RetrievePortfoliosConfigurationAsync(
        accountId,
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
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "account_id": "f2f7d2c7-d79b-4de2-ae9b-d01646259f9e",
  "allocation_id": "eb68abce-29f4-483f-a384-7f243e77851f",
  "rebalancing_strategy_ids": [
    "7a7322f5-b45d-4aba-aae4-c12797938c93",
    "fd8e5363-a89c-4a2f-b3c6-5f857721625f"
  ]
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


# Update Portfolios Configuration

Updates the allocation or rebalancing strategies of an existing portfolio configuration.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
UpdatePortfoliosConfigurationAsync(
    Guid accountId,
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PortfoliosConfigurationUpdateRequest body = null)
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
| `body` | [`PortfoliosConfigurationUpdateRequest`](../../doc/models/portfolios-configuration-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolio

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosConfiguration](../../doc/models/portfolios-configuration.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PortfoliosConfigurationUpdateRequest body = new PortfoliosConfigurationUpdateRequest
{
    AllocationId = new Guid("359e9c9e-b91c-4853-8674-e6657af609fc"),
};

try
{
    ApiResponse<PortfoliosConfiguration> result = await portfoliosApi.UpdatePortfoliosConfigurationAsync(
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
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-22T15:10:00.00Z",
  "account_id": "f2f7d2c7-d79b-4de2-ae9b-d01646259f9e",
  "allocation_id": "359e9c9e-b91c-4853-8674-e6657af609fc",
  "rebalancing_strategy_ids": [
    "7a7322f5-b45d-4aba-aae4-c12797938c93",
    "fd8e5363-a89c-4a2f-b3c6-5f857721625f"
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


# Delete Portfolios Configuration

Delete portfolios configuration (hard delete)

```csharp
DeletePortfoliosConfigurationAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**204**: Request has been processed successfully.

`Task`

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await portfoliosApi.DeletePortfoliosConfigurationAsync(
        accountId,
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
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Portfolios Orders

Returns a paginated list of portfolio orders for the authenticated client.

Portfolio orders represent buy or sell instructions that invest or withdraw a cash amount across all instruments in the account's current allocation.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for order lifecycle details.

```csharp
ListPortfoliosOrdersAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort23? sort = Models.Sort23.Id,
    Models.Order58? order = Models.Order58.Asc,
    int? offset = null,
    int? limit = 100,
    string startDate = null,
    string endDate = null,
    Guid? allocationId = null,
    Guid? accountId = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort23?`](../../doc/models/sort-23.md) | Query, Optional | Sort the result by `id`.<br><br>**Default**: `Sort23.id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `startDate` | `string` | Query, Optional | Returns portfolio orders with dates starting from and including this date (UTC) |
| `endDate` | `string` | Query, Optional | Returns portfolio orders with dates up until this date (UTC) |
| `allocationId` | `Guid?` | Query, Optional | Filters portfolio orders containing the allocation id |
| `accountId` | `Guid?` | Query, Optional | Filters portfolio orders containing the account id |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios orders

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosOrdersListResponse](../../doc/models/portfolios-orders-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort23? sort = Sort23.Id;
Order58? order = Order58.Asc;
int? limit = 100;
string startDate = "10/14/2022 10:10:10";
string endDate = "10/14/2022 10:10:10";
Guid? allocationId = new Guid("f96eaefd-b4c6-477a-9bbe-c744bf26172f");
Guid? accountId = new Guid("2c865aab-6737-4b0e-9154-69a4622b2727");
try
{
    ApiResponse<PortfoliosOrdersListResponse> result = await portfoliosApi.ListPortfoliosOrdersAsync(
        upvestClientId,
        upvestApiVersion,
        sort,
        order,
        null,
        limit,
        startDate,
        endDate,
        allocationId,
        accountId
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
      "created_at": "2021-07-21T14:10:00.000Z",
      "updated_at": "2021-07-21T14:10:00.000Z",
      "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
      "allocation_id": "eb68abce-29f4-483f-a384-7f243e77851f",
      "cash_amount": "1000",
      "currency": "EUR",
      "status": "PROCESSING",
      "type": "SELL",
      "post_tax": false,
      "client_reference": "",
      "initiation_flow": "API",
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
      ]
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


# Create Portfolios Order

Places a buy or sell portfolio order for the specified account, distributing the cash amount across instruments according to the account's allocation weights.

One underlying instrument order is generated per instrument in the allocation.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
CreatePortfoliosOrderAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PortfoliosOrderPlaceRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`PortfoliosOrderPlaceRequest`](../../doc/models/portfolios-order-place-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**200**: Portfolios order

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosOrder](../../doc/models/portfolios-order.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
PortfoliosOrderPlaceRequest body = new PortfoliosOrderPlaceRequest
{
    UserId = new Guid("4b9732bd-7496-4e91-8a5f-6360479d7fed"),
    AccountId = new Guid("00ef0be8-d564-43af-b3c7-11b7a2188030"),
    CashAmount = "100.00",
    Currency = Currency.Eur,
    Side = Side15.Buy,
};

try
{
    ApiResponse<PortfoliosOrder> result = await portfoliosApi.CreatePortfoliosOrderAsync(
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
  "created_at": "2021-07-21T14:10:00.000Z",
  "updated_at": "2021-07-21T14:10:00.000Z",
  "account_id": "debf2026-f2da-4ff0-bb84-92e45babb1e3",
  "allocation_id": "eb68abce-29f4-483f-a384-7f243e77851f",
  "cash_amount": "1000",
  "currency": "EUR",
  "status": "NEW",
  "type": "BUY",
  "post_tax": false,
  "client_reference": "",
  "initiation_flow": "API",
  "orders": []
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


# Retrieve Portfolios Order

Retrieves a portfolio order by its ID, including the status and details of each underlying instrument order.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for order lifecycle details.

```csharp
RetrievePortfoliosOrderAsync(
    Guid portfolioOrderId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `portfolioOrderId` | `Guid` | Template, Required | The unique identifier of the portfolio order. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios order

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosOrder](../../doc/models/portfolios-order.md).

## Example Usage

```csharp
Guid portfolioOrderId = new Guid("000013e8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<PortfoliosOrder> result = await portfoliosApi.RetrievePortfoliosOrderAsync(
        portfolioOrderId,
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
  "id": "29bc9a26-a6ee-47a3-b609-47d30a737afc",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "4b9732bd-7496-4e91-8a5f-6360479d7fed",
  "account_id": "f2f7d2c7-d79b-4de2-ae9b-d01646259f9e",
  "allocation_id": "19a902ac-1c01-4c45-bd5d-62fd496805c8",
  "cash_amount": "100.00",
  "currency": "EUR",
  "status": "PROCESSING",
  "type": "BUY",
  "client_reference": "fd1ae99e-772a-11ee-b962-0242ac120002",
  "initiation_flow": "SAVINGS_PLAN",
  "post_tax": false,
  "orders": [
    {
      "id": "79f2c816-13ed-4588-85e6-f3398b0b5825",
      "side": "BUY",
      "status": "NEW"
    },
    {
      "id": "44f50a4e-7ce8-4eae-a078-f4b3d8ab4708",
      "side": "BUY",
      "status": "NEW"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Cancel Portfolios Order

Cancel portfolios order. Cancellations may not be possible - in rare situations, some individual orders within portfolios order may already have been filled. In this case, cancellation could result in a mix of cancelled and filled orders.

```csharp
CancelPortfoliosOrderAsync(
    Guid portfolioOrderId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `portfolioOrderId` | `Guid` | Template, Required | The unique identifier of the portfolio order. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid portfolioOrderId = new Guid("000013e8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await portfoliosApi.CancelPortfoliosOrderAsync(
        portfolioOrderId,
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


# List Portfolios Allocation Accounts

Returns a paginated list of accounts associated with a specific portfolio allocation.

See the Portfolios guide ([TOL](https://docs.upvest.co/products/tol/guides/portfolios) / [BYOL](https://docs.upvest.co/products/byol/guides/portfolios)) for implementation details.

```csharp
ListPortfoliosAllocationAccountsAsync(
    Guid allocationId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort23? sort = Models.Sort23.Id,
    Models.Order58? order = Models.Order58.Asc,
    int? offset = null,
    int? limit = 100)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `allocationId` | `Guid` | Template, Required | The unique identifier of the portfolio allocation. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort23?`](../../doc/models/sort-23.md) | Query, Optional | Sort the result by `id`.<br><br>**Default**: `Sort23.id` |
| `order` | [`Order58?`](../../doc/models/order-58.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. By default, only ASC for ascending sort.<br><br>**Default**: `Order58.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`portfolios:admin`, `portfolios:read`

## Response Type

**200**: Portfolios allocation accounts

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.PortfoliosAllocationAccountsListResponse](../../doc/models/portfolios-allocation-accounts-list-response.md).

## Example Usage

```csharp
Guid allocationId = new Guid("00001ad8-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort23? sort = Sort23.Id;
Order58? order = Order58.Asc;
int? limit = 100;
try
{
    ApiResponse<PortfoliosAllocationAccountsListResponse> result = await portfoliosApi.ListPortfoliosAllocationAccountsAsync(
        allocationId,
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
    "total_count": 1,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "81d6e710-37e6-4a5a-9877-ef5bb6d717e3"
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

