# Instruments

All instrument related paths.

```csharp
InstrumentsApi instrumentsApi = client.InstrumentsApi;
```

## Class Name

`InstrumentsApi`

## Methods

* [List Instruments](../../doc/controllers/instruments.md#list-instruments)
* [Retrieve Instrument](../../doc/controllers/instruments.md#retrieve-instrument)


# List Instruments

List instruments

```csharp
ListInstrumentsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.InstrumentTradingStatus? tradingStatus = null,
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
| `tradingStatus` | [`InstrumentTradingStatus?`](../../doc/models/instrument-trading-status.md) | Query, Optional | Filters the list to only show instruments with a certain status (e.g. only instruments that can be currently traded). |
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | Sort the result by `created_at` or `updated_at`.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`instruments:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.InstrumentsListResponse](../../doc/models/instruments-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
InstrumentTradingStatus? tradingStatus = InstrumentTradingStatus.Active;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<InstrumentsListResponse> result = await instrumentsApi.ListInstrumentsAsync(
        upvestClientId,
        upvestApiVersion,
        tradingStatus,
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
    "total_count": 10,
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "created_at": "2022-08-31T17:28:00.00Z",
      "updated_at": "2022-08-31T17:28:00.00Z",
      "isin": "DE0007664005",
      "wkn": "766400",
      "name": "Volkswagen (VW) St. Aktie.",
      "fractional_trading": true,
      "trading_status": "ACTIVE"
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


# Retrieve Instrument

Returns the instrument.

```csharp
RetrieveInstrumentAsync(
    RetrieveInstrumentInstrumentId instrumentId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `instrumentId` | [`RetrieveInstrumentInstrumentId`](../../doc/models/containers/retrieve-instrument-instrument-id.md) | Template, Required | This is a container for one-of cases. |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`instruments:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.InstrumentsResponse](../../doc/models/instruments-response.md).

## Example Usage

```csharp
RetrieveInstrumentInstrumentId instrumentId = RetrieveInstrumentInstrumentId.FromString("String7");

Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<InstrumentsResponse> result = await instrumentsApi.RetrieveInstrumentAsync(
        instrumentId,
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
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "created_at": "2022-08-31T17:28:00.00Z",
  "updated_at": "2022-08-31T17:28:00.00Z",
  "isin": "DE0007664005",
  "wkn": "766400",
  "name": "Volkswagen (VW) St. Aktie.",
  "fractional_trading": true,
  "trading_status": "ACTIVE"
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

