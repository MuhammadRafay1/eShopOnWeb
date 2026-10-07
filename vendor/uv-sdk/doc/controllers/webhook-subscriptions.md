# Webhook Subscriptions

```csharp
WebhookSubscriptionsApi webhookSubscriptionsApi = client.WebhookSubscriptionsApi;
```

## Class Name

`WebhookSubscriptionsApi`

## Methods

* [Get Jwks](../../doc/controllers/webhook-subscriptions.md#get-jwks)
* [List Webhooks](../../doc/controllers/webhook-subscriptions.md#list-webhooks)
* [Create Webhook](../../doc/controllers/webhook-subscriptions.md#create-webhook)
* [Retrieve Webhook](../../doc/controllers/webhook-subscriptions.md#retrieve-webhook)
* [Delete Webhook](../../doc/controllers/webhook-subscriptions.md#delete-webhook)
* [Update Webhook](../../doc/controllers/webhook-subscriptions.md#update-webhook)
* [Test Webhook](../../doc/controllers/webhook-subscriptions.md#test-webhook)


# Get Jwks

Returns a list of signing keys used to verify webhooks.

```csharp
GetJwksAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`webhooks:admin`, `webhooks:read`

## Response Type

**200**: JWKS signing public keys

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.AuthVerificationKeys](../../doc/models/auth-verification-keys.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<AuthVerificationKeys> result = await webhookSubscriptionsApi.GetJwksAsync(
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
  "keys": [
    {
      "kid": "9ed77463-c18e-4794-ab5c-17423f9b801a",
      "kty": "EC",
      "crv": "P-521",
      "x": "AcYAf5Ou0UvtehpeRvyhnvvrUQvjI4B3VCxfSmxK4gqKJGxO_jTB-nTdrHbBfmZpwmwYR86DppMg43E579K5MQ54",
      "y": "ALkrId77iD0Ye2vBS5bTkLqPH-bo8urTpTXiNc6Ub-VOFbXfJrR0tQ8COvVXXu58BTu8jWV-1mtbjfJpDl3ISbcK"
    }
  ]
}
```

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# List Webhooks

Returns a list of all webhook subscriptions.

```csharp
ListWebhooksAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.Sort? sort = Models.Sort.CreatedAt,
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
| `sort` | [`Sort?`](../../doc/models/sort.md) | Query, Optional | Sort the result by `created_at`, `updated_at`, `title`, `url`, or `enabled`.<br><br>**Default**: `Sort.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`webhooks:admin`, `webhooks:read`

## Response Type

**200**: An object with a data property that contains an array of webhook subscription objects.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.WebhooksListResponse](../../doc/models/webhooks-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort? sort = Sort.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<WebhooksListResponse> result = await webhookSubscriptionsApi.ListWebhooksAsync(
        upvestClientId,
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
    "count": 1,
    "total_count": 1,
    "sort": "id",
    "order": "ASC"
  },
  "data": [
    {
      "id": "a8eb3540-5a84-40f9-b2bb-7f99f282fc5a",
      "created_at": "2021-07-21T14:10:00.00Z",
      "updated_at": "2021-07-21T14:10:00.00Z",
      "title": "User webhook",
      "url": "https://tenant.tld/webhooks/users",
      "type": [
        "USER"
      ],
      "enabled": false,
      "config": {
        "delay": "5s",
        "max_package_size": 10240
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
| 406 | Not Acceptable. The resource does not have a current representation that would be acceptable to the user agent. "Accept" header defined unsupported value. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Create Webhook

Creates a webhook subscription.

```csharp
CreateWebhookAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.WebhookCreateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`WebhookCreateRequest`](../../doc/models/webhook-create-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`webhooks:admin`

## Response Type

**201**: Returns a webhook subscription object if a valid webhook subscription object ID was provided.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Webhook](../../doc/models/webhook.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
WebhookCreateRequest body = new WebhookCreateRequest
{
    Title = "User webhook",
    Url = "https://tenant.tld/webhooks/users",
    Type = new List<UpvestInvestmentApi.Standard.Models.Type>
    {
        UpvestInvestmentApi.Standard.Models.Type.User,
    },
    Config = new Config
    {
        Delay = "1s",
        MaxPackageSize = 51200,
    },
};

try
{
    ApiResponse<Webhook> result = await webhookSubscriptionsApi.CreateWebhookAsync(
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
  "id": "a8eb3540-5a84-40f9-b2bb-7f99f282fc5a",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "title": "User webhook",
  "url": "https://tenant.tld/webhooks/users",
  "type": [
    "USER"
  ],
  "enabled": false,
  "config": {
    "delay": "5s",
    "max_package_size": 10240
  }
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


# Retrieve Webhook

Returns a webhook subscription specified by its ID.

```csharp
RetrieveWebhookAsync(
    Guid webhookId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `webhookId` | `Guid` | Template, Required | The unique identifier of the webhook subscription. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`webhooks:admin`, `webhooks:read`

## Response Type

**200**: Returns a webhook subscription object if a valid webhook subscription object ID was provided.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Webhook](../../doc/models/webhook.md).

## Example Usage

```csharp
Guid webhookId = new Guid("00000dc6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<Webhook> result = await webhookSubscriptionsApi.RetrieveWebhookAsync(
        webhookId,
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
  "id": "a8eb3540-5a84-40f9-b2bb-7f99f282fc5a",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "title": "User webhook",
  "url": "https://tenant.tld/webhooks/users",
  "type": [
    "USER"
  ],
  "enabled": true,
  "config": {
    "delay": "5s",
    "max_package_size": 1024
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


# Delete Webhook

Deletes a webhook subscription specified by its ID.

```csharp
DeleteWebhookAsync(
    Guid webhookId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `webhookId` | `Guid` | Template, Required | The unique identifier of the webhook subscription. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`webhooks:admin`

## Response Type

**204**: Request has been processed successfully.

`Task`

## Example Usage

```csharp
Guid webhookId = new Guid("00000dc6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    await webhookSubscriptionsApi.DeleteWebhookAsync(
        webhookId,
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

## Errors

| HTTP Status Code | Error Description | Exception Class |
|  --- | --- | --- |
| 401 | Unauthorized. The caller has not been authenticated. | [`ErrorException`](../../doc/models/error-exception.md) |
| 403 | Forbidden. The caller has been authenticated but is not allowed to take the requested action. | [`ErrorException`](../../doc/models/error-exception.md) |
| 404 | Not Found. The requested resource could not be found. | [`ErrorException`](../../doc/models/error-exception.md) |
| 429 | Too Many Requests. The caller has exceeded their quota for the time period and has been throttled. | [`ErrorException`](../../doc/models/error-exception.md) |
| 500 | Internal Server Error. The service encountered an unexpected error. | [`ErrorException`](../../doc/models/error-exception.md) |
| 503 | Service Unavailable. The service handling for this request cannot be reached at this time. | [`ErrorException`](../../doc/models/error-exception.md) |
| 504 | Gateway Timeout. The service gateway has reached its internal timeout. | [`ErrorException`](../../doc/models/error-exception.md) |


# Update Webhook

Updates a webhook subscription specified by its ID.

```csharp
UpdateWebhookAsync(
    Guid webhookId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.WebhookUpdateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `webhookId` | `Guid` | Template, Required | The unique identifier of the webhook subscription. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`WebhookUpdateRequest`](../../doc/models/webhook-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`webhooks:admin`

## Response Type

**200**: Returns a webhook subscription object if a valid webhook subscription object ID was provided.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.Webhook](../../doc/models/webhook.md).

## Example Usage

```csharp
Guid webhookId = new Guid("00000dc6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
WebhookUpdateRequest body = new WebhookUpdateRequest
{
    Url = "https://tenant.tld/webhooks/users",
    Type = new List<UpvestInvestmentApi.Standard.Models.Type>
    {
        UpvestInvestmentApi.Standard.Models.Type.User,
    },
};

try
{
    ApiResponse<Webhook> result = await webhookSubscriptionsApi.UpdateWebhookAsync(
        webhookId,
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
  "id": "a8eb3540-5a84-40f9-b2bb-7f99f282fc5a",
  "created_at": "2021-07-21T14:10:00.00Z",
  "updated_at": "2021-07-21T14:10:00.00Z",
  "title": "User webhook",
  "url": "https://tenant.tld/webhooks/users",
  "type": [
    "USER"
  ],
  "enabled": true,
  "config": {
    "delay": "1s",
    "max_package_size": 51200
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


# Test Webhook

Tests a webhook subscription specified by its ID.

```csharp
TestWebhookAsync(
    Guid webhookId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `webhookId` | `Guid` | Template, Required | The unique identifier of the webhook subscription. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`webhooks:admin`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.WebhooksTestResponse](../../doc/models/webhooks-test-response.md).

## Example Usage

```csharp
Guid webhookId = new Guid("00000dc6-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<WebhooksTestResponse> result = await webhookSubscriptionsApi.TestWebhookAsync(
        webhookId,
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
  "url": "https://api.example.com/test",
  "response": {
    "status": 200,
    "headers": {
      "Content-Type": "application/json",
      "X-Request-ID": "abc123"
    },
    "body": ""
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

