# Fees

All fees related paths.

```csharp
FeesApi feesApi = client.FeesApi;
```

## Class Name

`FeesApi`

## Methods

* [List Fee Collections](../../doc/controllers/fees.md#list-fee-collections)
* [Create Fee Collection](../../doc/controllers/fees.md#create-fee-collection)
* [Retrieve Fee Collection](../../doc/controllers/fees.md#retrieve-fee-collection)


# List Fee Collections

Returns a list of fee collections.

```csharp
ListFeeCollectionsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Guid? accountId = null,
    Guid? accountGroupId = null,
    Models.Sort1? sort = Models.Sort1.CreatedAt,
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
| `accountId` | `Guid?` | Query, Optional | Filters the list to show only fees associated with a specific account ID. |
| `accountGroupId` | `Guid?` | Query, Optional | Filters the list to show only fees associated with a specific account group ID. |
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | Field of resource to sort by.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`fees:admin`, `fees:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FeeCollectionListResponse](../../doc/models/fee-collection-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Guid? accountId = new Guid("413715f2-5401-4b97-8055-034a6b879f8c");
Guid? accountGroupId = new Guid("72da9e41-9926-419f-976e-4c8069a04249");
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<FeeCollectionListResponse> result = await feesApi.ListFeeCollectionsAsync(
        upvestClientId,
        upvestApiVersion,
        accountId,
        accountGroupId,
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
    "count": 3,
    "total_count": 3,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "d77e6924-9bb7-4db0-addc-404442258f4b",
      "created_at": "2022-11-30T14:16:22Z",
      "updated_at": "2022-11-30T14:16:22Z",
      "account_id": "689f7566-2ca3-4007-b99d-e501be8c7783",
      "account_group_id": "999f7566-2ca3-4007-b99d-e501be8c7222",
      "type": "SERVICE_FEE",
      "collection_amount": "12.41",
      "processed_amount": {
        "cash_balance": "6.50",
        "sell_to_cover": "0"
      },
      "currency": "EUR",
      "status": "PROCESSING",
      "period_start": "2022-11-01",
      "period_end": "2022-11-30"
    },
    {
      "id": "7c5f2b11-7078-4bb9-8ed8-9dcf5eb7597f",
      "created_at": "2022-11-30T14:16:22Z",
      "updated_at": "2022-11-30T14:16:22Z",
      "account_id": "5b8b1e66-cf3c-40f3-94d3-c94f2ac7abb7",
      "account_group_id": "87805651-ad25-4a85-8c04-88a666164ad4",
      "type": "SERVICE_FEE",
      "collection_amount": "82.12",
      "processed_amount": {
        "cash_balance": "40.00",
        "sell_to_cover": "42.12"
      },
      "currency": "EUR",
      "status": "FINALISED",
      "period_start": "2022-09-01",
      "period_end": "2022-12-31"
    },
    {
      "id": "85fba2a0-da2f-46ac-a2a8-b4e7c66cb375",
      "created_at": "2022-11-30T14:16:22Z",
      "updated_at": "2022-11-30T14:16:22Z",
      "account_id": "c28b6611-7ac1-405a-8eb7-ab3b35caccc2",
      "account_group_id": "f930f9d4-3e21-4ad5-a266-730821d9a9fc",
      "type": "SERVICE_FEE_LIQUIDATION",
      "collection_amount": "65.30",
      "processed_amount": {
        "cash_balance": "0.00",
        "sell_to_cover": "0.00"
      },
      "currency": "EUR",
      "status": "CANCELLED",
      "period_start": "2022-03-01",
      "period_end": "2022-07-30"
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Fee Collection

Creates a fee collection for pre-calculated fee amounts.

```csharp
CreateFeeCollectionAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.FeeCollectionCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`FeeCollectionCreateRequest`](../../doc/models/fee-collection-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`fees:admin`

## Response Type

**200**: Fee collection created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FeeCollection](../../doc/models/fee-collection.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
FeeCollectionCreateRequest body = new FeeCollectionCreateRequest
{
    AccountId = new Guid("689f7566-2ca3-4007-b99d-e501be8c7783"),
    Type = Type37.ServiceFee,
    CollectionAmount = "12.41",
    Currency = Currency.Eur,
    PeriodStart = DateTime.Parse("2022-11-01"),
    PeriodEnd = DateTime.Parse("2022-11-30"),
};

try
{
    ApiResponse<FeeCollection> result = await feesApi.CreateFeeCollectionAsync(
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
  "id": "d77e6924-9bb7-4db0-addc-404442258f4b",
  "created_at": "2022-11-30T14:16:22Z",
  "updated_at": "2022-11-30T14:16:22Z",
  "account_id": "689f7566-2ca3-4007-b99d-e501be8c7783",
  "account_group_id": "999f7566-2ca3-4007-b99d-e501be8c7222",
  "type": "SERVICE_FEE",
  "collection_amount": "12.41",
  "processed_amount": {
    "cash_balance": "6.50",
    "sell_to_cover": "0"
  },
  "currency": "EUR",
  "status": "PROCESSING",
  "period_start": "2022-11-01",
  "period_end": "2022-11-30"
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


# Retrieve Fee Collection

Returns the fee collection specified by its ID.

```csharp
RetrieveFeeCollectionAsync(
    Guid feeCollectionId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `feeCollectionId` | `Guid` | Template, Required | The unique identifier of the fee collection. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`fees:admin`, `fees:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.FeeCollection](../../doc/models/fee-collection.md).

## Example Usage

```csharp
Guid feeCollectionId = new Guid("0000187e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<FeeCollection> result = await feesApi.RetrieveFeeCollectionAsync(
        feeCollectionId,
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
  "id": "d77e6924-9bb7-4db0-addc-404442258f4b",
  "created_at": "2022-11-30T14:16:22Z",
  "updated_at": "2022-11-30T14:16:22Z",
  "account_id": "689f7566-2ca3-4007-b99d-e501be8c7783",
  "account_group_id": "999f7566-2ca3-4007-b99d-e501be8c7222",
  "type": "SERVICE_FEE",
  "collection_amount": "12.41",
  "processed_amount": {
    "cash_balance": "6.50",
    "sell_to_cover": "0"
  },
  "currency": "EUR",
  "status": "PROCESSING",
  "period_start": "2022-11-01",
  "period_end": "2022-11-30",
  "calculation_breakdown": [
    {
      "fee_model_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "subperiod_start": "2022-11-01",
      "subperiod_end": "2022-11-30",
      "subtotal_amount": "12.41",
      "components": [
        {
          "type": "SERVICE",
          "amount": "10.00"
        },
        {
          "type": "PLATFORM",
          "amount": "2.41"
        }
      ]
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

