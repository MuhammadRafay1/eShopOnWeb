# User Identifiers

```csharp
UserIdentifiersApi userIdentifiersApi = client.UserIdentifiersApi;
```

## Class Name

`UserIdentifiersApi`

## Methods

* [List User Identifiers](../../doc/controllers/user-identifiers.md#list-user-identifiers)
* [Create Identifier](../../doc/controllers/user-identifiers.md#create-identifier)
* [Retrieve Identifier](../../doc/controllers/user-identifiers.md#retrieve-identifier)
* [Update Identifier](../../doc/controllers/user-identifiers.md#update-identifier)


# List User Identifiers

Lists all existing identifiers of a user used for transaction reporting.

```csharp
ListUserIdentifiersAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`users:admin`, `users:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.IdentifiersListResponse](../../doc/models/identifiers-list-response.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<IdentifiersListResponse> result = await userIdentifiersApi.ListUserIdentifiersAsync(
        userId,
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
      "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be203",
      "type": "NATIONAL_ID",
      "issuing_country": "PL",
      "identifier_standard": "PESEL",
      "identifier": "83053078917"
    },
    {
      "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be111",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be203",
      "type": "NATIONAL_ID",
      "issuing_country": "DK",
      "identifier_standard": "DPIC",
      "identifier": "3005837891"
    },
    {
      "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be303",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be505",
      "type": "NATIONAL_ID",
      "issuing_country": "DE",
      "identifier_standard": "CONCAT",
      "identifier": ""
    },
    {
      "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be303",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be505",
      "type": "PASSPORT",
      "issuing_country": "NL",
      "identifier_standard": "NLPN",
      "identifier": ""
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


# Create Identifier

Creates a new identifier for a user that will be used for transaction reporting obligations. This identifier is required for user activation if the user's nationalities do not allow reporting using the CONCAT format.

See the User identifiers guide ([TOL](https://docs.upvest.co/products/tol/guides/users/users_identifiers) / [BYOL](https://docs.upvest.co/products/byol/guides/users/users_identifiers)) for details.

```csharp
CreateIdentifierAsync(
    Guid userId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.IdentifierCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`IdentifierCreateRequest`](../../doc/models/identifier-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`users:admin`

## Response Type

**200**: The user identifier is created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Identifier](../../doc/models/identifier.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
IdentifierCreateRequest body = new IdentifierCreateRequest
{
    Type = Type7.NationalId,
    IssuingCountry = IssuingCountry.Pl,
    Identifier = "83053078917",
    IdentifierStandard = "PESEL",
};

try
{
    ApiResponse<Identifier> result = await userIdentifiersApi.CreateIdentifierAsync(
        userId,
        upvestClientId,
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
  "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be203",
  "type": "NATIONAL_ID",
  "issuing_country": "PL",
  "identifier_standard": "PESEL",
  "identifier": "83053078917"
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


# Retrieve Identifier

Returns an existing identifier of a given user used for transaction reporting.

```csharp
RetrieveIdentifierAsync(
    Guid userId,
    Guid identifierId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `identifierId` | `Guid` | Template, Required | The unique identifier of the user identifier document. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`users:admin`, `users:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Identifier](../../doc/models/identifier.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid identifierId = new Guid("000011cc-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<Identifier> result = await userIdentifiersApi.RetrieveIdentifierAsync(
        userId,
        identifierId,
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
  "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be203",
  "type": "NATIONAL_ID",
  "issuing_country": "PL",
  "identifier_standard": "PESEL",
  "identifier": "83053078917"
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


# Update Identifier

Updates an existing identifier of a given user used for transaction reporting.

```csharp
UpdateIdentifierAsync(
    Guid userId,
    Guid identifierId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.IdentifierUpdateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `userId` | `Guid` | Template, Required | The unique identifier of the user. Universally Unique Identifier (UUID). |
| `identifierId` | `Guid` | Template, Required | The unique identifier of the user identifier document. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`IdentifierUpdateRequest`](../../doc/models/identifier-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`users:admin`

## Response Type

**200**: The user identifier is updated.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Identifier](../../doc/models/identifier.md).

## Example Usage

```csharp
Guid userId = new Guid("00001e80-0000-0000-0000-000000000000");
Guid identifierId = new Guid("000011cc-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
IdentifierUpdateRequest body = new IdentifierUpdateRequest
{
    Identifier = "83053078111",
};

try
{
    ApiResponse<Identifier> result = await userIdentifiersApi.UpdateIdentifierAsync(
        userId,
        identifierId,
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

## Example Response *(as JSON)*

```json
{
  "id": "83d83ec2-d2ca-49ff-bbea-b92b5c3be202",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-22T15:20:10.00Z",
  "user_id": "00000ec2-d2ca-49ff-bbea-b92b5c3be203",
  "type": "NATIONAL_ID",
  "issuing_country": "PL",
  "identifier_standard": "PESEL",
  "identifier": "83053078111"
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

