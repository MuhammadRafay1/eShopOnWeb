# Securities Transfers

```csharp
SecuritiesTransfersApi securitiesTransfersApi = client.SecuritiesTransfersApi;
```

## Class Name

`SecuritiesTransfersApi`

## Methods

* [List Securities Transfers](../../doc/controllers/securities-transfers.md#list-securities-transfers)
* [Create Securities Transfer](../../doc/controllers/securities-transfers.md#create-securities-transfer)


# List Securities Transfers

List securities transfers

```csharp
ListSecuritiesTransfersAsync(
    Guid upvestClientId,
    Models.Direction3 direction,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort1? sort = Models.Sort1.CreatedAt,
    Models.Order? order = Models.Order.Asc,
    int? offset = null,
    int? limit = 100,
    Guid? userId = null,
    Guid? accountId = null,
    Guid? accountGroupId = null,
    Models.Status82? status = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `direction` | [`Direction3`](../../doc/models/direction-3.md) | Query, Required | Filter the list by transfer direction. |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | Sort the result by `created_at`, `updated_at`.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |
| `userId` | `Guid?` | Query, Optional | Filters results by user ID. Universally Unique Identifier (UUID). |
| `accountId` | `Guid?` | Query, Optional | Filters results by account ID. Universally Unique Identifier (UUID). |
| `accountGroupId` | `Guid?` | Query, Optional | Filters results by account group ID. Universally Unique Identifier (UUID). |
| `status` | [`Status82?`](../../doc/models/status-82.md) | Query, Optional | Status of the securities transfer<br><br>* NEW - Securities transfer is created but not started processing.<br>* PROCESSING - Securities transfer is in processing.<br>* SETTLED - Securities transfer was successfully settled.<br>* CANCELLED - Securities transfer was cancelled. |

## Requires scope

### oauth-client-credentials

`securities_transfers:admin`, `securities_transfers:read`

## Response Type

**200**: Securities Transfers list

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SecurityTransfersListResponse](../../doc/models/security-transfers-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Direction3 direction = Direction3.Incoming;
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<SecurityTransfersListResponse> result = await securitiesTransfersApi.ListSecuritiesTransfersAsync(
        upvestClientId,
        direction,
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
    "count": 2,
    "total_count": 2,
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "9637e68f-7065-4131-a072-6d57044ebd8c",
      "created_at": "2024-01-22T14:10:00.00Z",
      "updated_at": "2024-01-22T14:12:34.56Z",
      "direction": "INCOMING",
      "status": "NEW",
      "transfer_type": "NO_OWNER_CHANGE",
      "instrument_id": "US0378331005",
      "instrument_id_type": "ISIN",
      "quantity": "10",
      "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
      "account_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
      "settlement_reference": "b82dc5985558fdr2",
      "counterparty": {
        "type": "BIC",
        "id": "DUMBZZ33XXX",
        "account_number": "1234567891",
        "name": "Max Mustermann"
      },
      "settlement_counterparties": {
        "settlement_agent": {
          "identification": {
            "type": "BIC",
            "value": "FAKEZZ55XXX"
          },
          "account": {
            "type": "SAFE",
            "value": "DAKV1234"
          }
        },
        "settlement_party": {
          "identification": {
            "type": "BIC",
            "value": "DUMBZZ33XXX"
          },
          "account": {
            "type": "SAFE",
            "value": "1234567891"
          }
        }
      },
      "place_of_settlement": "DAKVDEFFXXX",
      "trade_date": "2024-01-22",
      "settlement_date": "2024-01-23",
      "order_date": "2024-01-20"
    },
    {
      "id": "64464124-18a2-4886-bc9f-c1b41fa8e4df",
      "created_at": "2025-01-22T14:10:00.00Z",
      "updated_at": "2025-01-22T14:12:34.56Z",
      "direction": "INCOMING",
      "status": "SETTLED",
      "transfer_type": "NO_OWNER_CHANGE",
      "instrument_id": "US88160R1014",
      "instrument_id_type": "ISIN",
      "quantity": "5",
      "quantity_settled": "5",
      "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
      "account_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
      "settlement_reference": "b82dc5985558fdr2",
      "counterparty": {
        "type": "BIC",
        "id": "DUMBZZ33XXX",
        "account_number": "1234567891",
        "name": "Max Mustermann"
      },
      "settlement_counterparties": {
        "settlement_agent": {
          "identification": {
            "type": "BIC",
            "value": "FAKEZZ55XXX"
          },
          "account": {
            "type": "SAFE",
            "value": "DAKV1234"
          }
        },
        "settlement_party": {
          "identification": {
            "type": "BIC",
            "value": "DUMBZZ33XXX"
          },
          "account": {
            "type": "SAFE",
            "value": "1234567891"
          }
        }
      },
      "place_of_settlement": "CEDELULLXXX",
      "trade_date": "2024-01-22",
      "settlement_date": "2024-01-23",
      "actual_settlement_date": "2024-01-24",
      "order_date": "2023-12-20",
      "delay_reason": "COUNTERPARTY_NOT_RESPONDING"
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


# Create Securities Transfer

Create securities transfer

```csharp
CreateSecuritiesTransferAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TransfersSecuritiesTransferCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TransfersSecuritiesTransferCreateRequest`](../../doc/models/transfers-securities-transfer-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`securities_transfers:admin`

## Response Type

**200**: Securities Transfer created

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.SecuritiesTransfersResponse](../../doc/models/securities-transfers-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
TransfersSecuritiesTransferCreateRequest body = new TransfersSecuritiesTransferCreateRequest
{
    Direction = Direction4.Incoming,
    InstrumentId = "US0378331005",
    InstrumentIdType = "ISIN",
    Quantity = "10",
    TransferType = "NO_OWNER_CHANGE",
    UserId = new Guid("7e9a0529-d289-4f4a-ae01-c2cd013d582e"),
    AccountId = new Guid("d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4"),
    SettlementReference = "b82dc5985558fdr2",
    Counterparty = new SecuritiesTransferCounterpartyBic
    {
        Type = "BIC",
        Id = "DUMBZZ33XXX",
        AccountNumber = "1234567891",
        Name = "Max Mustermann",
    },
    OrderDate = DateTime.Parse("2024-01-20"),
};

try
{
    ApiResponse<SecuritiesTransfersResponse> result = await securitiesTransfersApi.CreateSecuritiesTransferAsync(
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
  "id": "9637e68f-7065-4131-a072-6d57044ebd8c",
  "created_at": "2024-01-22T14:10:00.00Z",
  "updated_at": "2024-01-22T14:12:34.56Z",
  "direction": "INCOMING",
  "status": "NEW",
  "transfer_type": "NO_OWNER_CHANGE",
  "instrument_id": "US0378331005",
  "instrument_id_type": "ISIN",
  "quantity": "10",
  "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
  "account_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
  "settlement_reference": "b82dc5985558fdr2",
  "counterparty": {
    "type": "BIC",
    "id": "DUMBZZ33XXX",
    "account_number": "1234567891",
    "name": "Max Mustermann"
  },
  "settlement_counterparties": {
    "settlement_agent": {
      "identification": {
        "type": "BIC",
        "value": "FAKEZZ55XXX"
      },
      "account": {
        "type": "SAFE",
        "value": "DAKV1234"
      }
    },
    "settlement_party": {
      "identification": {
        "type": "BIC",
        "value": "DUMBZZ33XXX"
      },
      "account": {
        "type": "SAFE",
        "value": "1234567891"
      }
    }
  },
  "place_of_settlement": "DAKVDEFFXXX",
  "trade_date": "2024-01-22",
  "settlement_date": "2024-01-23",
  "order_date": "2024-01-20"
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

