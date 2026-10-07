# Account Transfers

```csharp
AccountTransfersApi accountTransfersApi = client.AccountTransfersApi;
```

## Class Name

`AccountTransfersApi`

## Methods

* [Create Account Transfer](../../doc/controllers/account-transfers.md#create-account-transfer)
* [Retrieve Account Transfer](../../doc/controllers/account-transfers.md#retrieve-account-transfer)


# Create Account Transfer

Create account transfer

```csharp
CreateAccountTransferAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.TransfersAccountTransferCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`TransfersAccountTransferCreateRequest`](../../doc/models/transfers-account-transfer-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`account_transfers:admin`

## Response Type

**200**: Account Transfer created

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountTransfersResponse](../../doc/models/account-transfers-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
TransfersAccountTransferCreateRequest body = new TransfersAccountTransferCreateRequest
{
    Direction = Direction.Incoming,
    UserId = new Guid("7e9a0529-d289-4f4a-ae01-c2cd013d582e"),
    AccountId = new Guid("d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4"),
    TransferType = "NO_OWNER_CHANGE",
    SettlementReference = "b82dc5985558fdr2",
    Instruments = new List<InstrumentsRequest>
    {
        new InstrumentsRequest
        {
            Id = "US0378331005",
            IdType = "ISIN",
            Quantity = "10",
        },
        new InstrumentsRequest
        {
            Id = "US64110L1061",
            IdType = "ISIN",
            Quantity = "5",
        },
    },
    Counterparty = new AccountTransferCounterpartyBic
    {
        Type = "BIC",
        Id = "ABCDDEFFXXX",
        AccountNumber = "1234567891",
        Name = "Max Mustermann",
    },
    OrderDate = DateTime.Parse("2025-01-21"),
};

try
{
    ApiResponse<AccountTransfersResponse> result = await accountTransfersApi.CreateAccountTransferAsync(
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
  "id": "63da4452-2dd3-4414-a7ca-66c0e3e89645",
  "created_at": "2025-01-22T14:10:00.00Z",
  "updated_at": "2025-01-22T14:12:34.56Z",
  "direction": "INCOMING",
  "status": "NEW",
  "transfer_type": "NO_OWNER_CHANGE",
  "instruments": [
    {
      "id": "US0378331005",
      "id_type": "ISIN",
      "quantity": "10",
      "status": "NEW",
      "transfer_id": "9637e68f-7065-4131-a072-6d57044ebd8c"
    },
    {
      "id": "US64110L1061",
      "id_type": "ISIN",
      "quantity": "5",
      "status": "NEW",
      "transfer_id": "c2f84524-3f05-4849-909c-2998803327d3"
    }
  ],
  "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
  "account_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
  "settlement_reference": "b82dc5985558fdr2",
  "counterparty": {
    "type": "BIC",
    "id": "ABCDDEFFXXX",
    "account_number": "1234567891",
    "name": "Max Mustermann"
  },
  "order_date": "2025-01-21"
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


# Retrieve Account Transfer

Retrieve account transfer

```csharp
RetrieveAccountTransferAsync(
    Guid accountTransferId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `accountTransferId` | `Guid` | Template, Required | The unique identifier of the account transfer. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`account_transfers:admin`, `account_transfers:read`

## Response Type

**200**: Account Transfer object

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AccountTransfersResponse](../../doc/models/account-transfers-response.md).

## Example Usage

```csharp
Guid accountTransferId = new Guid("00000488-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AccountTransfersResponse> result = await accountTransfersApi.RetrieveAccountTransferAsync(
        accountTransferId,
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
  "id": "63da4452-2dd3-4414-a7ca-66c0e3e89645",
  "created_at": "2025-01-22T14:10:00.00Z",
  "updated_at": "2025-01-22T14:12:34.56Z",
  "direction": "INCOMING",
  "status": "SETTLED",
  "transfer_type": "NO_OWNER_CHANGE",
  "instruments": [
    {
      "id": "US0378331005",
      "id_type": "ISIN",
      "quantity": "10",
      "status": "SETTLED",
      "transfer_id": "9637e68f-7065-4131-a072-6d57044ebd8c"
    },
    {
      "id": "US64110L1061",
      "id_type": "ISIN",
      "quantity": "5",
      "status": "SETTLED",
      "transfer_id": "c2f84524-3f05-4849-909c-2998803327d3"
    }
  ],
  "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
  "account_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
  "settlement_reference": "b82dc5985558fdr2",
  "counterparty": {
    "type": "BIC",
    "id": "ABCDDEFFXXX",
    "account_number": "1234567891",
    "name": "Max Mustermann"
  },
  "order_date": "2025-01-21",
  "delay_reason": "COUNTERPARTY_NOT_RESPONDING"
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

