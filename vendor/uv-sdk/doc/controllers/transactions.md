# Transactions

All transactions related paths.

```csharp
TransactionsApi transactionsApi = client.TransactionsApi;
```

## Class Name

`TransactionsApi`

## Methods

* [List Cash Transactions](../../doc/controllers/transactions.md#list-cash-transactions)
* [List Securities Transactions](../../doc/controllers/transactions.md#list-securities-transactions)


# List Cash Transactions

List cash transactions

```csharp
ListCashTransactionsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Guid? accountGroupId = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.Sort29? sort = Models.Sort29.BookingDate,
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
| `accountGroupId` | `Guid?` | Query, Optional | Filters the list to show only transactions associated with a certain account group ID. |
| `startDate` | `DateTime?` | Query, Optional | - |
| `endDate` | `DateTime?` | Query, Optional | - |
| `sort` | [`Sort29?`](../../doc/models/sort-29.md) | Query, Optional | Sort the result by `booking_date`.<br><br>**Default**: `Sort29.booking_date` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`transactions:read`

## Response Type

**200**: Cash Transactions

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.CashTransactionListResponse](../../doc/models/cash-transaction-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Guid? accountGroupId = new Guid("413715f2-5401-4b97-8055-034a6b879f8c");
DateTime? startDate = DateTime.Parse("2023-01-03");
DateTime? endDate = DateTime.Parse("2023-01-11");
Sort29? sort = Sort29.BookingDate;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<CashTransactionListResponse> result = await transactionsApi.ListCashTransactionsAsync(
        upvestClientId,
        upvestApiVersion,
        accountGroupId,
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
    "count": 2,
    "total_count": 2,
    "sort": "booking_date",
    "order": "ASC"
  },
  "data": [
    {
      "id": "6dc9fbca-8835-11ed-a217-2eabd0c03f8a",
      "created_at": "2023-01-01T00:00:00.000Z",
      "updated_at": "2023-01-01T00:00:00.000Z",
      "type": "ORDER_EXECUTION",
      "account_group_id": "6409e3f2-8835-11ed-96a4-2eabd0c03f8a",
      "delta": {
        "amount": "-305.00",
        "currency": "EUR"
      },
      "taxes": [
        {
          "amount": "5.00",
          "currency": "EUR",
          "type": "TOTAL"
        }
      ],
      "taxes_details": {
        "total_amount": {
          "amount": "5.00",
          "currency": "EUR"
        },
        "tax_breakdown": [
          {
            "amount": "5.00",
            "currency": "EUR",
            "type": "FINANCIAL_TRANSACTION_TAX",
            "taxing_jurisdiction": "FR"
          }
        ]
      },
      "references": [
        {
          "id": "7579a672-8835-11ed-9455-2eabd0c03f8a",
          "type": "ORDER_EXECUTION"
        },
        {
          "id": "1f5758d3-1ef7-4b4c-96ec-6b3da2bf1a8a",
          "type": "ORDER"
        }
      ],
      "booking_date": "2023-01-01T00:00:00.000Z",
      "value_date": "2023-01-01T00:00:00.000Z"
    },
    {
      "id": "9e7a3188-eff8-11ed-a05b-0242ac120003",
      "created_at": "2023-01-01T00:00:00.000Z",
      "updated_at": "2023-01-01T00:00:00.000Z",
      "type": "SEPA_DIRECT_DEBIT",
      "account_group_id": "6409e3f2-8835-11ed-96a4-2eabd0c03f8a",
      "delta": {
        "amount": "100.00",
        "currency": "EUR"
      },
      "taxes": [],
      "taxes_details": {
        "total_amount": {
          "amount": "0.00",
          "currency": "EUR"
        },
        "tax_breakdown": []
      },
      "references": [
        {
          "id": "98f01200-eff8-11ed-a05b-0242ac120003",
          "type": "DIRECT_DEBIT"
        }
      ],
      "booking_date": "2023-01-01T00:00:00.000Z",
      "value_date": "2023-01-01T00:00:00.000Z"
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


# List Securities Transactions

List securities transactions

```csharp
ListSecuritiesTransactionsAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Guid? accountGroupId = null,
    Guid? accountId = null,
    DateTime? startDate = null,
    DateTime? endDate = null,
    Models.Sort29? sort = Models.Sort29.BookingDate,
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
| `accountGroupId` | `Guid?` | Query, Optional | Filters the list to only show transactions associated with a certain account group ID. |
| `accountId` | `Guid?` | Query, Optional | Filters the list to only show transactions associated with a certain account ID. |
| `startDate` | `DateTime?` | Query, Optional | - |
| `endDate` | `DateTime?` | Query, Optional | - |
| `sort` | [`Sort29?`](../../doc/models/sort-29.md) | Query, Optional | Sort the result by `booking_date`.<br><br>**Default**: `Sort29.booking_date` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`transactions:read`

## Response Type

**200**: Securities Transactions

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SecurityTransactionListResponse](../../doc/models/security-transaction-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Guid? accountGroupId = new Guid("413715f2-5401-4b97-8055-034a6b879f8c");
Guid? accountId = new Guid("413715f2-5401-4b97-8055-034a6b879f8c");
DateTime? startDate = DateTime.Parse("2023-01-03");
DateTime? endDate = DateTime.Parse("2023-01-11");
Sort29? sort = Sort29.BookingDate;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<SecurityTransactionListResponse> result = await transactionsApi.ListSecuritiesTransactionsAsync(
        upvestClientId,
        upvestApiVersion,
        accountGroupId,
        accountId,
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
    "count": 2,
    "total_count": 2,
    "sort": "booking_date",
    "order": "ASC"
  },
  "data": [
    {
      "id": "6dc9fbca-8835-11ed-a217-2eabd0c03f8a",
      "created_at": "2023-01-01T00:00:00.000Z",
      "updated_at": "2023-01-01T00:00:00.000Z",
      "type": "ORDER_EXECUTION",
      "account_id": "db6290bb-1491-45bf-aafe-262dad59d497",
      "account_group_id": "6409e3f2-8835-11ed-96a4-2eabd0c03f8a",
      "instrument": {
        "uuid": "ccb86937-8a39-4160-8d33-85bf9e902321",
        "isin": "US0378331005"
      },
      "delta": {
        "amount": "-2.5234543879"
      },
      "references": [
        {
          "id": "7579a672-8835-11ed-9455-2eabd0c03f8a",
          "type": "ORDER_EXECUTION"
        },
        {
          "id": "1f5758d3-1ef7-4b4c-96ec-6b3da2bf1a8a",
          "type": "ORDER"
        }
      ],
      "booking_date": "2023-01-01T00:00:00.000Z",
      "value_date": "2023-01-01T00:00:00.000Z"
    },
    {
      "id": "6dc9fbca-8835-11ed-a217-2eabd0c03f8a",
      "created_at": "2023-01-01T00:00:00.000Z",
      "updated_at": "2023-01-01T00:00:00.000Z",
      "type": "ORDER_EXECUTION_CANCELLATION",
      "account_id": "db6290bb-1491-45bf-aafe-262dad59d497",
      "account_group_id": "6409e3f2-8835-11ed-96a4-2eabd0c03f8a",
      "instrument": {
        "uuid": "ccb86937-8a39-4160-8d33-85bf9e902321",
        "isin": "US0378331005"
      },
      "delta": {
        "amount": "2.5234543879"
      },
      "references": [
        {
          "id": "7579a672-8835-11ed-9455-2eabd0c03f8a",
          "type": "ORDER_EXECUTION"
        },
        {
          "id": "1f5758d3-1ef7-4b4c-96ec-6b3da2bf1a8a",
          "type": "ORDER"
        }
      ],
      "booking_date": "2023-01-01T00:00:00.000Z",
      "value_date": "2023-01-01T00:00:00.000Z"
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

