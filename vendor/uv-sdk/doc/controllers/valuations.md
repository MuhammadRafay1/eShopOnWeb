# Valuations

All valuations related paths.

```csharp
ValuationsApi valuationsApi = client.ValuationsApi;
```

## Class Name

`ValuationsApi`

## Methods

* [Get Account Valuation](../../doc/controllers/valuations.md#get-account-valuation)
* [List Account Valuation History](../../doc/controllers/valuations.md#list-account-valuation-history)


# Get Account Valuation

Get current valuation for an account

```csharp
GetAccountValuationAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.PriceQuality3 priceQuality,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountId` | `Guid` | Template, Required | The unique identifier of the account. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `priceQuality` | [`PriceQuality3`](../../doc/models/price-quality-3.md) | Query, Required | - |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`valuations:read`

## Response Type

**200**: Valuations

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountValuation](../../doc/models/account-valuation.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
PriceQuality3 priceQuality = PriceQuality3.Eod;
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AccountValuation> result = await valuationsApi.GetAccountValuationAsync(
        accountId,
        upvestClientId,
        priceQuality,
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
  "id": "404b170a-9042-11ed-9a51-2eabd0c03f8a",
  "created_at": "2023-01-10T14:15:22Z",
  "updated_at": "2023-01-10T14:15:22Z",
  "valuation_time": "2023-01-10T14:15:22Z",
  "account_id": "51cdc0cc-9042-11ed-b017-2eabd0c03f8a",
  "price_quality": "EOD",
  "total_security_value": {
    "amount": "142.29",
    "currency": "EUR"
  },
  "security_positions": [
    {
      "instrument": {
        "uuid": "123e4567-e89b-12d3-a456-426614174000",
        "isin": "DE0007664005"
      },
      "value": {
        "amount": "104.81",
        "currency": "EUR",
        "price_time": "2023-01-09T09:20:00Z"
      },
      "quantity": "0.65",
      "weight": "0.736595",
      "price_quality": "EOD"
    },
    {
      "instrument": {
        "uuid": "48b798b7-3a62-4f30-8307-ee94d35b21b7",
        "isin": "FR0010524777"
      },
      "value": {
        "amount": "37.48",
        "currency": "EUR",
        "price_time": "2023-01-09T09:20:00Z"
      },
      "quantity": "1.00",
      "weight": "0.263405",
      "price_quality": "EOD"
    },
    {
      "instrument": {
        "uuid": "7499ccf8-6fa5-43ef-af9e-dd062e10ab59",
        "isin": "SE0019889775"
      },
      "quantity": "5.00",
      "value": null,
      "weight": null,
      "price_quality": "NA"
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


# List Account Valuation History

List valuation history for an account

```csharp
ListAccountValuationHistoryAsync(
    Guid accountId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.Sort28? sort = Models.Sort28.ValuationTime,
    Models.Order? order = Models.Order.Asc,
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
| `startDate` | `DateTime?` | Query, Optional | - |
| `endDate` | `DateTime?` | Query, Optional | - |
| `sort` | [`Sort28?`](../../doc/models/sort-28.md) | Query, Optional | Sort the result by `valuation_time`.<br><br>**Default**: `Sort28.valuation_time` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`valuations:read`

## Response Type

**200**: Valuations

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountValuationListResponse](../../doc/models/account-valuation-list-response.md).

## Example Usage

```csharp
Guid accountId = new Guid("00001114-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
DateTime? startDate = DateTime.Parse("2023-01-03");
DateTime? endDate = DateTime.Parse("2023-01-11");
Sort28? sort = Sort28.ValuationTime;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<AccountValuationListResponse> result = await valuationsApi.ListAccountValuationHistoryAsync(
        accountId,
        upvestClientId,
        upvestApiVersion,
        startDate,
        endDate,
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
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "404b170a-9042-11ed-9a51-2eabd0c03f8a",
      "created_at": "2023-01-10T14:15:22Z",
      "updated_at": "2023-01-10T14:15:22Z",
      "valuation_time": "2023-01-10T14:15:22Z",
      "account_id": "51cdc0cc-9042-11ed-b017-2eabd0c03f8a",
      "price_quality": "EOD",
      "total_security_value": {
        "amount": "142.29",
        "currency": "EUR"
      },
      "security_positions": [
        {
          "instrument": {
            "uuid": "123e4567-e89b-12d3-a456-426614174000",
            "isin": "DE0007664005"
          },
          "value": {
            "amount": "104.81",
            "currency": "EUR",
            "price_time": "2023-01-09T09:20:00Z"
          },
          "quantity": "0.65",
          "weight": "0.736595"
        },
        {
          "instrument": {
            "uuid": "48b798b7-3a62-4f30-8307-ee94d35b21b7",
            "isin": "FR0010524777"
          },
          "value": {
            "amount": "37.48",
            "currency": "EUR",
            "price_time": "2023-01-09T09:20:00Z"
          },
          "quantity": "1.00",
          "weight": "0.263405"
        },
        {
          "instrument": {
            "uuid": "7499ccf8-6fa5-43ef-af9e-dd062e10ab59",
            "isin": "SE0019889775"
          },
          "quantity": "5.00",
          "value": null,
          "weight": null
        }
      ]
    },
    {
      "id": "adfbf024-3d31-11ef-b99b-2eabd0c03f8a",
      "created_at": "2023-01-11T14:15:22Z",
      "updated_at": "2023-01-11T14:15:22Z",
      "valuation_time": "2023-01-11T14:15:22Z",
      "account_id": "51cdc0cc-9042-11ed-b017-2eabd0c03f8a",
      "price_quality": "EOD",
      "total_security_value": {
        "amount": "104.81",
        "currency": "EUR"
      },
      "security_positions": [
        {
          "instrument": {
            "uuid": "123e4567-e89b-12d3-a456-426614174000",
            "isin": "DE0007664005"
          },
          "value": {
            "amount": "104.81",
            "currency": "EUR",
            "price_time": "2023-01-10T09:20:00Z"
          },
          "quantity": "0.65",
          "weight": "1"
        }
      ]
    },
    {
      "id": "be38000e-3d31-11ef-ac4e-2eabd0c03f8a",
      "created_at": "2023-01-12T14:15:22Z",
      "updated_at": "2023-01-12T14:15:22Z",
      "valuation_time": "2023-01-12T14:15:22Z",
      "account_id": "51cdc0cc-9042-11ed-b017-2eabd0c03f8a",
      "price_quality": "EOD",
      "total_security_value": {
        "amount": "0",
        "currency": "EUR"
      },
      "security_positions": []
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

