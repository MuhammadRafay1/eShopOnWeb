# ISA Transfers

```csharp
IsaTransfersApi isaTransfersApi = client.IsaTransfersApi;
```

## Class Name

`IsaTransfersApi`


# Create Isa Transfer

Create ISA transfer

```csharp
CreateIsaTransferAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.IsaTransfersRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`IsaTransfersRequest`](../../doc/models/isa-transfers-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`isa_transfers:admin`

## Response Type

**200**: ISA Transfer created

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.IsaTransfersResponse](../../doc/models/isa-transfers-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
IsaTransfersRequest body = new IsaTransfersRequest
{
    Direction = Direction7.Incoming,
    TransferType = TransferType.IsaExternal,
    Currency = "GBP",
    TransferMethod = "CASH",
    UserId = new Guid("2dedfeb0-58cd-55f2-ae08-0e41fe0413d9"),
    AccountGroupId = new Guid("48043cd0-e306-4b48-bbdd-6ca5d0967009"),
    TransferDate = DateTime.ParseExact("2025-10-10T10:00:00Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    TransferValue = "10000.00",
    Reference = "4XD8Z53CST3LGEZBYO",
    Counterparty = new Counterparty2
    {
        AccountNumber = "5WJSZB3C8",
    },
    Details = new Details2
    {
        CurrentYearSubscription = new CurrentYearSubscription
        {
            TransferAmount = "2000.00",
            FirstSubscriptionAt = DateTime.ParseExact("2025-09-09T12:50:00Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
    },
};

try
{
    ApiResponse<IsaTransfersResponse> result = await isaTransfersApi.CreateIsaTransferAsync(
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
  "id": "9637e68f-7065-4131-a072-6d57044ebd8c",
  "created_at": "2024-01-22T14:10:00.00Z",
  "updated_at": "2024-01-22T14:12:34.56Z",
  "direction": "INCOMING",
  "status": "NEW",
  "transfer_type": "ISA_INTERNAL",
  "user_id": "7e9a0529-d289-4f4a-ae01-c2cd013d582e",
  "account_group_id": "d396b7c3-aa67-488a-8e39-4a1a7cbeb9f4",
  "reference": "b82dc5985558fdr2",
  "transfer_value": "10000.00",
  "currency": "GBP",
  "transfer_method": "CASH",
  "counterparty": {
    "account_number": "1234567891"
  },
  "details": {
    "current_year_subscription": {
      "transfer_amount": "2000.00",
      "first_subscription_at": "2024-01-15T10:00:00.00Z"
    }
  },
  "transfer_date": "2024-01-22T14:10:00.00Z"
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

