# Reports

All reports related paths.

```csharp
ReportsApi reportsApi = client.ReportsApi;
```

## Class Name

`ReportsApi`

## Methods

* [List User Reports](../../doc/controllers/reports.md#list-user-reports)
* [Create Report](../../doc/controllers/reports.md#create-report)
* [Retrieve Report](../../doc/controllers/reports.md#retrieve-report)
* [List Business Reports](../../doc/controllers/reports.md#list-business-reports)


# List User Reports

List user reports

```csharp
ListUserReportsAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.ReportType? type = null,
    ListUserReportsInstrument instrument = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.Sort22? sort = Models.Sort22.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? limit = 100,
    int? offset = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `type` | [`ReportType?`](../../doc/models/report-type.md) | Query, Optional | Filters the list to only show reports of a certain type (e.g. only buy order confirmations) |
| `instrument` | [`ListUserReportsInstrument`](../../doc/models/containers/list-user-reports-instrument.md) | Query, Optional | This is a container for one-of cases. |
| `startDate` | `DateTime?` | Query, Optional | - |
| `endDate` | `DateTime?` | Query, Optional | - |
| `sort` | [`Sort22?`](../../doc/models/sort-22.md) | Query, Optional | Field of resource to sort by<br><br>**Default**: `Sort22.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`reports:admin`, `reports:read`

## Response Type

**200**: Reports list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.ReportsListResponse](../../doc/models/reports-list-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
ReportType? type = ReportType.BuyOrder;
ListUserReportsInstrument instrument = ListUserReportsInstrument.FromString("uuid:ebabcf4d-61c3-4942-875c-e265a7c2d062");

DateTime? startDate = DateTime.Parse("2020-08-21");
DateTime? endDate = DateTime.Parse("2020-09-25");
Sort22? sort = Sort22.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<ReportsListResponse> result = await reportsApi.ListUserReportsAsync(
        userId,
        upvestClientId,
        upvestApiVersion,
        type,
        instrument,
        startDate,
        endDate,
        sort,
        order,
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
      "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
      "created_at": "2020-08-24T14:15:22Z",
      "user_id": "d1a4be99-8bb6-4e78-b897-8168f6823ab5",
      "type": "CORPORATE_ACTION_CASH_TRANSACTION",
      "substituted_report_id": null,
      "data": {
        "account": {
          "id": "a9a4ad54-6dd1-419a-a98d-ab48f9f23bc8"
        },
        "references": [
          {
            "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
            "type": "CORPORATE_ACTION_TRANSACTION_ID"
          }
        ]
      }
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


# Create Report

Create a user report

```csharp
CreateReportAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateReportBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateReportBody`](../../doc/models/containers/create-report-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`reports:admin`

## Response Type

**200**: Report

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type CreateReportResponse.

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
CreateReportBody body = CreateReportBody.FromReportOrderExAnteCostCreateRequestRegular(
    new ReportOrderExAnteCostCreateRequestRegular
    {
        Type = "ORDER_EX_ANTE_COST",
        Order = ReportOrderExAnteCostCreateRequestRegularOrder.FromExAnteCostBusinessOrder(
            new ExAnteCostBusinessOrder
            {
                BusinessId = new Guid("d1a4be99-8bb6-4e78-b897-8168f6823ab5"),
                AccountId = new Guid("c5034305-c441-4711-adbf-93cfbc13a695"),
                Currency = Currency.Eur,
                Side = Side8.Sell,
                InstrumentId = "LU0274208692",
                InstrumentIdType = "ISIN",
                OrderType = OrderType4.Market,
                CashAmount = "1000.00",
            }
        ),
        Fees = new List<ReportOrderExAnteCostCreateRequestRegularFees>
        {
            ReportOrderExAnteCostCreateRequestRegularFees.FromAbsoluteFee4(
                new AbsoluteFee4
                {
                    Type = FeeType8.TransactionFeeBuy,
                    ValueType = "ABSOLUTE",
                    CashAmount = "1.00",
                    Currency = Currency.Eur,
                }
            ),
            ReportOrderExAnteCostCreateRequestRegularFees.FromTransactionFee(
                new TransactionFee
                {
                    Type = FeeType.TransactionFeeSell,
                    TransactionFeeModelId = new Guid("eb5ba93f-5dfe-4bf1-84da-0caacc80c111"),
                }
            ),
            ReportOrderExAnteCostCreateRequestRegularFees.FromRelativeFee(
                new RelativeFee
                {
                    Type = FeeType8.AnnualAumBasedFee,
                    ValueType = "RELATIVE",
                    Bps = "10",
                }
            ),
        },
    }
);

try
{
    ApiResponse<CreateReportResponse> result = await reportsApi.CreateReportAsync(
        upvestClientId,
        null,
        body
    );
    result.Data.Match<VoidType>(
        userReportOrderExAnteCost: userReportOrderExAnteCost =>
        {
            // TODO: handle userReportOrderExAnteCost here
            Console.WriteLine(userReportOrderExAnteCost);
            return null;
        },
        businessReportOrderExAnteCost: businessReportOrderExAnteCost =>
        {
            // TODO: handle businessReportOrderExAnteCost here
            Console.WriteLine(businessReportOrderExAnteCost);
            return null;
        });
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

## Example Response

```
{
  "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
  "created_at": "2023-08-24T14:15:22Z",
  "business_id": "d1a4be99-8bb6-4e78-b897-8168f6823ab5",
  "type": "ORDER_EX_ANTE_COST",
  "substituted_report_id": null,
  "data": {
    "account": {
      "id": "c5034305-c441-4711-adbf-93cfbc13a695",
      "account_number": 1
    },
    "account_group": {
      "id": "9d95820d-4333-46b6-98de-04ab7512e76f",
      "securities_account_number": "123456789"
    },
    "business": {
      "company_name": "Acme Corporation",
      "address": {
        "address_line1": "Rosenweg 221",
        "address_line2": "apt. 33",
        "postcode": "45678",
        "city": "Berlin",
        "country": "DE"
      }
    },
    "instrument": {
      "isin": "LU0274208692",
      "short_name": "XTR.MSCI WORLD SWAP 1C"
    },
    "venue": {
      "name": "Tradegate"
    },
    "order": {
      "business_id": "d1a4be99-8bb6-4e78-b897-8168f6823ab5",
      "instrument_id": "LU0274208692",
      "instrument_id_type": "ISIN",
      "account_id": "c5034305-c441-4711-adbf-93cfbc13a695",
      "order_type": "MARKET",
      "side": "BUY",
      "quantity": "15",
      "cash_amount": "1220.85",
      "currency": "EUR",
      "price": "81.39"
    },
    "holding_period": {
      "unit": "YEAR",
      "quantity": 3
    },
    "total_cost": {
      "product": {
        "cash_amount": "17.48",
        "currency": "EUR",
        "as_percentage": "1.43"
      },
      "service": {
        "cash_amount": "6.88",
        "currency": "EUR",
        "as_percentage": "0.56"
      },
      "third_party": {
        "cash_amount": "0.70",
        "currency": "EUR",
        "as_percentage": "0.06"
      },
      "total": {
        "cash_amount": "25.06",
        "currency": "EUR",
        "as_percentage": "2.05"
      }
    },
    "product_cost": {
      "one_off": {
        "cash_amount": "0.20",
        "currency": "EUR",
        "as_percentage": "0.02"
      },
      "ongoing": {
        "cash_amount": "16.48",
        "currency": "EUR",
        "as_percentage": "1.35"
      },
      "transaction": {
        "cash_amount": "0.80",
        "currency": "EUR",
        "as_percentage": "0.07"
      },
      "incidental": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "total": {
        "cash_amount": "17.48",
        "currency": "EUR",
        "as_percentage": "1.43"
      }
    },
    "service_cost": {
      "one_off": {
        "cash_amount": "0.49",
        "currency": "EUR",
        "as_percentage": "0.04"
      },
      "ongoing": {
        "cash_amount": "4.39",
        "currency": "EUR",
        "as_percentage": "0.36"
      },
      "transaction": {
        "cash_amount": "2.00",
        "currency": "EUR",
        "as_percentage": "0.16"
      },
      "ancillary": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "incidental": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "total": {
        "cash_amount": "6.88",
        "currency": "EUR",
        "as_percentage": "0.56"
      }
    },
    "third_party_payments": {
      "total": {
        "cash_amount": "0.70",
        "currency": "EUR",
        "as_percentage": "0.06"
      },
      "received_by_client": {
        "cash_amount": "0.35",
        "currency": "EUR",
        "as_percentage": "0.03"
      },
      "received_by_upvest": {
        "cash_amount": "0.35",
        "currency": "EUR",
        "as_percentage": "0.03"
      }
    },
    "return_impact": {
      "year_one": {
        "cash_amount": "9.05",
        "currency": "EUR",
        "as_percentage": "0.74"
      },
      "year_two": {
        "cash_amount": "6.96",
        "currency": "EUR",
        "as_percentage": "0.57"
      },
      "year_three": {
        "cash_amount": "8.36",
        "currency": "EUR",
        "as_percentage": "0.68"
      }
    }
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


# Retrieve Report

Retrieve a report for either a natural person or a business entity.

```csharp
RetrieveReportAsync(
    Guid reportId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Accept? accept = Models.Accept.EnumApplicationjson)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `reportId` | `Guid` | Template, Required | The unique identifier of the report. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `accept` | [`Accept?`](../../doc/models/accept.md) | Header, Optional | Report format<br><br>**Default**: `Accept.Enum_applicationjson` |

## Requires scope

### oauth-client-credentials

`reports:admin`, `reports:read`

## Response Type

**200**: Report

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveReportResponse.

## Example Usage

```csharp
Guid reportId = new Guid("0000067e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Accept? accept = Accept.EnumApplicationjson;
try
{
    ApiResponse<RetrieveReportResponse> result = await reportsApi.RetrieveReportAsync(
        reportId,
        upvestClientId,
        upvestApiVersion,
        accept
    );
    result.Data.Match<VoidType>(
        userReport: userReport =>
        {
            // TODO: handle userReport here
            Console.WriteLine(userReport);
            return null;
        },
        businessReport: businessReport =>
        {
            // TODO: handle businessReport here
            Console.WriteLine(businessReport);
            return null;
        },
        userReportOrderExAnteCost: userReportOrderExAnteCost =>
        {
            // TODO: handle userReportOrderExAnteCost here
            Console.WriteLine(userReportOrderExAnteCost);
            return null;
        },
        businessReportOrderExAnteCost: businessReportOrderExAnteCost =>
        {
            // TODO: handle businessReportOrderExAnteCost here
            Console.WriteLine(businessReportOrderExAnteCost);
            return null;
        });
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

## Example Response

```
{
  "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
  "created_at": "2023-08-24T14:15:22Z",
  "business_id": "d1a4be99-8bb6-4e78-b897-8168f6823ab5",
  "type": "ORDER_EX_ANTE_COST",
  "substituted_report_id": null,
  "data": {
    "account": {
      "id": "c5034305-c441-4711-adbf-93cfbc13a695",
      "account_number": 1
    },
    "account_group": {
      "id": "9d95820d-4333-46b6-98de-04ab7512e76f",
      "securities_account_number": "123456789"
    },
    "business": {
      "company_name": "Acme Corporation",
      "address": {
        "address_line1": "Rosenweg 221",
        "address_line2": "apt. 33",
        "postcode": "45678",
        "city": "Berlin",
        "country": "DE"
      }
    },
    "instrument": {
      "isin": "LU0274208692",
      "short_name": "XTR.MSCI WORLD SWAP 1C"
    },
    "venue": {
      "name": "Tradegate"
    },
    "order": {
      "business_id": "d1a4be99-8bb6-4e78-b897-8168f6823ab5",
      "instrument_id": "LU0274208692",
      "instrument_id_type": "ISIN",
      "account_id": "c5034305-c441-4711-adbf-93cfbc13a695",
      "order_type": "MARKET",
      "side": "BUY",
      "quantity": "15",
      "cash_amount": "1220.85",
      "currency": "EUR",
      "price": "81.39"
    },
    "holding_period": {
      "unit": "YEAR",
      "quantity": 3
    },
    "total_cost": {
      "product": {
        "cash_amount": "17.48",
        "currency": "EUR",
        "as_percentage": "1.43"
      },
      "service": {
        "cash_amount": "6.88",
        "currency": "EUR",
        "as_percentage": "0.56"
      },
      "third_party": {
        "cash_amount": "0.70",
        "currency": "EUR",
        "as_percentage": "0.06"
      },
      "total": {
        "cash_amount": "25.06",
        "currency": "EUR",
        "as_percentage": "2.05"
      }
    },
    "product_cost": {
      "one_off": {
        "cash_amount": "0.20",
        "currency": "EUR",
        "as_percentage": "0.02"
      },
      "ongoing": {
        "cash_amount": "16.48",
        "currency": "EUR",
        "as_percentage": "1.35"
      },
      "transaction": {
        "cash_amount": "0.80",
        "currency": "EUR",
        "as_percentage": "0.07"
      },
      "incidental": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "total": {
        "cash_amount": "17.48",
        "currency": "EUR",
        "as_percentage": "1.43"
      }
    },
    "service_cost": {
      "one_off": {
        "cash_amount": "0.49",
        "currency": "EUR",
        "as_percentage": "0.04"
      },
      "ongoing": {
        "cash_amount": "4.39",
        "currency": "EUR",
        "as_percentage": "0.36"
      },
      "transaction": {
        "cash_amount": "2.00",
        "currency": "EUR",
        "as_percentage": "0.16"
      },
      "ancillary": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "incidental": {
        "cash_amount": "0.00",
        "currency": "EUR",
        "as_percentage": "0.00"
      },
      "total": {
        "cash_amount": "6.88",
        "currency": "EUR",
        "as_percentage": "0.56"
      }
    },
    "third_party_payments": {
      "total": {
        "cash_amount": "0.70",
        "currency": "EUR",
        "as_percentage": "0.06"
      },
      "received_by_client": {
        "cash_amount": "0.35",
        "currency": "EUR",
        "as_percentage": "0.03"
      },
      "received_by_upvest": {
        "cash_amount": "0.35",
        "currency": "EUR",
        "as_percentage": "0.03"
      }
    },
    "return_impact": {
      "year_one": {
        "cash_amount": "9.05",
        "currency": "EUR",
        "as_percentage": "0.74"
      },
      "year_two": {
        "cash_amount": "6.96",
        "currency": "EUR",
        "as_percentage": "0.57"
      },
      "year_three": {
        "cash_amount": "8.36",
        "currency": "EUR",
        "as_percentage": "0.68"
      }
    }
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


# List Business Reports

Returns a paginated list of reports generated for the business identified by `business_id`.

Use the `offset` and `limit` query parameters to page through results; `meta.total_count` gives the total number of matching reports. Use `type` and `instrument` to filter results, and `start_date`/`end_date` to limit the range of report generation dates.

```csharp
ListBusinessReportsAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.ReportType? type = null,
    ListBusinessReportsInstrument instrument = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.Sort4? sort = Models.Sort4.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? limit = 100,
    int? offset = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `type` | [`ReportType?`](../../doc/models/report-type.md) | Query, Optional | Filters the list to only show reports of a certain type (e.g. only buy order confirmations) |
| `instrument` | [`ListBusinessReportsInstrument`](../../doc/models/containers/list-business-reports-instrument.md) | Query, Optional | This is a container for one-of cases. |
| `startDate` | `DateTime?` | Query, Optional | Returns reports generated on or after this date, as an [RFC 3339](https://datatracker.ietf.org/doc/html/rfc3339) date in `YYYY-MM-DD` format (UTC) |
| `endDate` | `DateTime?` | Query, Optional | Returns reports generated on or before this date, as an [RFC 3339](https://datatracker.ietf.org/doc/html/rfc3339) date in `YYYY-MM-DD` format (UTC) |
| `sort` | [`Sort4?`](../../doc/models/sort-4.md) | Query, Optional | Field to sort the result by. Accepts `id` or `created_at`<br><br>**Default**: `Sort4.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |

## Requires scope

### oauth-client-credentials

`reports:admin`, `reports:read`

## Response Type

**200**: Reports list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.BusinessReportsListResponse](../../doc/models/business-reports-list-response.md).

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
ReportType? type = ReportType.BuyOrder;
ListBusinessReportsInstrument instrument = ListBusinessReportsInstrument.FromString("uuid:ebabcf4d-61c3-4942-875c-e265a7c2d062");

DateTime? startDate = DateTime.Parse("2020-08-21");
DateTime? endDate = DateTime.Parse("2020-09-25");
Sort4? sort = Sort4.Id;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<BusinessReportsListResponse> result = await reportsApi.ListBusinessReportsAsync(
        businessId,
        upvestClientId,
        upvestApiVersion,
        type,
        instrument,
        startDate,
        endDate,
        sort,
        order,
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
      "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
      "created_at": "2020-08-24T14:15:22Z",
      "business_id": "a9a72268-4f3c-4de2-abb9-a553a3bb7608",
      "type": "CORPORATE_ACTION_CASH_TRANSACTION",
      "substituted_report_id": null,
      "data": {
        "account": {
          "id": "a9a4ad54-6dd1-419a-a98d-ab48f9f23bc8"
        },
        "references": [
          {
            "id": "b96b1ee7-d491-43eb-b5e4-4833af9c9c2f",
            "type": "CORPORATE_ACTION_TRANSACTION_ID"
          }
        ]
      }
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

