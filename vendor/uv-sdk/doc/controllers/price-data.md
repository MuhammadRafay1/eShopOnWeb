# Price Data

```csharp
PriceDataApi priceDataApi = client.PriceDataApi;
```

## Class Name

`PriceDataApi`

## Methods

* [Retrieve Instrument Venues](../../doc/controllers/price-data.md#retrieve-instrument-venues)
* [Retrieve Instrument Latest Price](../../doc/controllers/price-data.md#retrieve-instrument-latest-price)
* [Retrieve Instrument Prices Ohlc](../../doc/controllers/price-data.md#retrieve-instrument-prices-ohlc)


# Retrieve Instrument Venues

The venues where the instrument is traded and for which the price data is available.

```csharp
RetrieveInstrumentVenuesAsync(
    RetrieveInstrumentVenuesInstrumentId instrumentId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `instrumentId` | [`RetrieveInstrumentVenuesInstrumentId`](../../doc/models/containers/retrieve-instrument-venues-instrument-id.md) | Template, Required | This is a container for one-of cases. |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`prices:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.InstrumentsVenuesResponse](../../doc/models/instruments-venues-response.md).

## Example Usage

```csharp
RetrieveInstrumentVenuesInstrumentId instrumentId = RetrieveInstrumentVenuesInstrumentId.FromString("String7");

Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<InstrumentsVenuesResponse> result = await priceDataApi.RetrieveInstrumentVenuesAsync(
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
  "data": [
    {
      "id": "20d6024b-2df4-41ae-8d42-62e4744e455b",
      "name": "Tradegate",
      "price_qualities": [
        "EOD",
        "REALTIME"
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


# Retrieve Instrument Latest Price

Returns the instrument's latest price as available at the specified venue.

**NOTE**: Please note that in live mode we provide the latest prices that we receive from our data provider. However, it is to be expected that such updates will only take place on trading days.

```csharp
RetrieveInstrumentLatestPriceAsync(
    RetrieveInstrumentLatestPriceInstrumentId instrumentId,
    Guid upvestClientId,
    Guid venueId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.PriceQuality1? priceQuality = Models.PriceQuality1.HighestAvailable)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `instrumentId` | [`RetrieveInstrumentLatestPriceInstrumentId`](../../doc/models/containers/retrieve-instrument-latest-price-instrument-id.md) | Template, Required | This is a container for one-of cases. |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `venueId` | `Guid` | Template, Required | The unique identifier of the trading venue. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `priceQuality` | [`PriceQuality1?`](../../doc/models/price-quality-1.md) | Query, Optional | **Default**: `PriceQuality1.HIGHEST_AVAILABLE` |

## Requires scope

### oauth-client-credentials

`prices:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.InstrumentsVenuesPricesLatestResponse](../../doc/models/instruments-venues-prices-latest-response.md).

## Example Usage

```csharp
RetrieveInstrumentLatestPriceInstrumentId instrumentId = RetrieveInstrumentLatestPriceInstrumentId.FromString("String7");

Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid venueId = new Guid("000019da-0000-0000-0000-000000000000");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
PriceQuality1? priceQuality = PriceQuality1.HighestAvailable;
try
{
    ApiResponse<InstrumentsVenuesPricesLatestResponse> result = await priceDataApi.RetrieveInstrumentLatestPriceAsync(
        instrumentId,
        upvestClientId,
        venueId,
        upvestApiVersion,
        priceQuality
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
  "price_quality": "REALTIME",
  "currency": "EUR",
  "bids": [
    {
      "time": "2023-01-09T13:02:15Z",
      "price": "210.01",
      "size": "100"
    }
  ],
  "asks": [
    {
      "time": "2023-01-09T13:02:15Z",
      "price": "212.32",
      "size": "50"
    }
  ],
  "last_trade": {
    "time": "2023-01-09T12:59:04Z",
    "price": "211.32",
    "size": "40"
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


# Retrieve Instrument Prices Ohlc

Returns OHLC prices for the instrument at the specified venue for the chosen time period.

If requested interval is `1d`, then the response also includes days with 0 volume. In these cases `open`, `high`, `low` are empty (`""`), but `close` is provided. If requested interval is smaller than `1d`, then the intervals with 0 volume are omitted from the response.

**NOTE**: Please note that in live mode we provide the latest prices that we receive from our data provider. However, it is to be expected that such updates will only take place on trading days.

```csharp
RetrieveInstrumentPricesOhlcAsync(
    RetrieveInstrumentPricesOhlcInstrumentId instrumentId,
    Guid upvestClientId,
    Guid venueId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.AdjustedFor? adjustedFor = Models.AdjustedFor.All,
    string interval = "1d")
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `instrumentId` | [`RetrieveInstrumentPricesOhlcInstrumentId`](../../doc/models/containers/retrieve-instrument-prices-ohlc-instrument-id.md) | Template, Required | This is a container for one-of cases. |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `venueId` | `Guid` | Template, Required | The unique identifier of the trading venue. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `startDate` | `DateTime?` | Query, Optional | Returns OHLC prices from and including this datetime (UTC). If not specified, OHLC prices are returned from 30 days before the specified `end_date`. Time part of the timestamp is optional. |
| `endDate` | `DateTime?` | Query, Optional | Returns OHLC prices from and including this datetime (UTC). If not specified, OHLC prices are returned up to yesterday. Time part of the timestamp is optional. |
| `adjustedFor` | [`AdjustedFor?`](../../doc/models/adjusted-for.md) | Query, Optional | Indication of the desired data adjustment. Prices are adjusted for corporate actions such as cash dividends and stock splits. Does not apply if intraday prices are being requested.<br><br>**Default**: `AdjustedFor.ALL` |
| `interval` | `string` | Query, Optional | Indicates the maximum length of the interval each OHLC price tuple covers. If a price did not change subsequent price tuples are omitted. Allowed values are `1d` for daily prices or any positive integer followed by `h` or `m` for hour or minute, respectively. Requests are limited to a maximum of 1000 data points.<br><br>**Default**: `"1d"`<br><br>**Constraints**: *Pattern*: `^1d\|[1-9][0-9]*[hm]$` |

## Requires scope

### oauth-client-credentials

`prices:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.InstrumentsVenuesPricesOhlcResponse](../../doc/models/instruments-venues-prices-ohlc-response.md).

## Example Usage

```csharp
RetrieveInstrumentPricesOhlcInstrumentId instrumentId = RetrieveInstrumentPricesOhlcInstrumentId.FromString("String7");

Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid venueId = new Guid("000019da-0000-0000-0000-000000000000");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
DateTime? startDate = DateTime.Parse("11/01/2023 10:30:00");
DateTime? endDate = DateTime.Parse("11/15/2023 13:30:00");
AdjustedFor? adjustedFor = AdjustedFor.None;
string interval = "4h";
try
{
    ApiResponse<InstrumentsVenuesPricesOhlcResponse> result = await priceDataApi.RetrieveInstrumentPricesOhlcAsync(
        instrumentId,
        upvestClientId,
        venueId,
        upvestApiVersion,
        startDate,
        endDate,
        adjustedFor,
        interval
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
  "data": [
    {
      "currency": "EUR",
      "time": "2023-01-09T00:00:00Z",
      "open": "213.32",
      "high": "215.32",
      "low": "210.32",
      "close": "213.22",
      "volume": "277973"
    },
    {
      "currency": "EUR",
      "time": "2023-01-10T00:00:00Z",
      "open": "214.42",
      "high": "214.42",
      "low": "213.22",
      "close": "213.22",
      "volume": "260335"
    }
  ],
  "meta": {
    "count": 2
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

