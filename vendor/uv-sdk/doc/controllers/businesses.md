# Businesses

All Businesses related paths.

```csharp
BusinessesApi businessesApi = client.BusinessesApi;
```

## Class Name

`BusinessesApi`

## Methods

* [List Businesses](../../doc/controllers/businesses.md#list-businesses)
* [Create Business](../../doc/controllers/businesses.md#create-business)
* [Retrieve Business](../../doc/controllers/businesses.md#retrieve-business)
* [Business Data Change](../../doc/controllers/businesses.md#business-data-change)


# List Businesses

Returns a paginated list of all businesses.

Use `offset` and `limit` to paginate through results, and `sort`/`order` to control the ordering by `created_at` or `updated_at`.

See the Business accounts guide ([TOL](https://docs.upvest.co/products/tol/guides/accounts/businesses_overview) / [BYOL](https://docs.upvest.co/products/byol/guides/accounts/businesses_overview)) for business account types.

```csharp
ListBusinessesAsync(
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
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
| `sort` | [`Sort1?`](../../doc/models/sort-1.md) | Query, Optional | Sort the result by `created_at`, `updated_at`.<br><br>**Default**: `Sort1.created_at` |
| `order` | [`Order?`](../../doc/models/order.md) | Query, Optional | Sort order of the result list if the `sort` parameter is specified. Use `ASC` for ascending or `DESC` for descending sort order.<br><br>**Default**: `Order.ASC` |
| `offset` | `int?` | Query, Optional | Use the `offset` argument to specify where in the list of results to start when returning items for a particular query.<br><br>**Constraints**: `>= 0` |
| `limit` | `int?` | Query, Optional | Use the `limit` argument to specify the maximum number of items returned.<br><br>**Default**: `100`<br><br>**Constraints**: `>= 1`, `<= 1000` |

## Requires scope

### oauth-client-credentials

`businesses:admin`, `businesses:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type [Models.BusinessesListResponse](../../doc/models/businesses-list-response.md).

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
Sort1? sort = Sort1.CreatedAt;
Order? order = Order.Asc;
int? limit = 100;
try
{
    ApiResponse<BusinessesListResponse> result = await businessesApi.ListBusinessesAsync(
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
    "count": 0,
    "total_count": 0,
    "sort": "created_at",
    "order": "ASC"
  },
  "data": [
    {
      "id": "a9a72268-4f3c-4de2-abb9-a553a3bb7608",
      "created_at": "2023-09-24T11:15:10Z",
      "updated_at": "2023-09-24T11:15:10Z",
      "contact_email": "acme.corporation@example.com",
      "registered_address": {
        "address_line1": "110",
        "address_line2": "Schonhauser Allee",
        "postcode": "101110",
        "city": "Berlin",
        "state": "BE",
        "country": "DE"
      },
      "registration_number": "111221111",
      "tax_information": {
        "tax_country_code": "DE",
        "tax_identification_type": "STEUERNUMMER",
        "tax_identification_number": "12345678901",
        "is_resident_in_multiple_tax_jurisdictions": false,
        "is_subject_to_fatca": false
      },
      "business_type": "LIMITED_LIABILITY_COMPANY",
      "identification": {
        "company_name": "Acme Corporation",
        "incorporation_date": "2000-01-01",
        "district_court": "Berlin Charlottenburg",
        "legal_designation": "GESELLSCHAFT_MIT_BESCHRAENKTER_HAFTUNG",
        "legal_entity_identifier": "213800K98H63P249S632",
        "common_reporting_standards": {
          "is_passive_non_financial_entity": false,
          "is_financial_institution": false
        }
      },
      "terms_and_conditions": {
        "consent_document_id": "6a163d14-5b3d-48be-ac90-43314e96be71",
        "confirmed_at": "2023-10-24T14:14:22Z"
      },
      "data_privacy_and_sharing_agreement": {
        "consent_document_id": "fa2227c3-2b29-47e1-84da-996d09517edc",
        "confirmed_at": "2023-10-24T14:16:22Z"
      },
      "status": "ACTIVE"
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


# Create Business

Creates a business.

Supports both corporate and sole trader business types, each with a different set of required fields.

See the Business accounts guide ([TOL](https://docs.upvest.co/products/tol/guides/accounts/businesses_overview) / [BYOL](https://docs.upvest.co/products/byol/guides/accounts/businesses_overview)) for implementation details.

```csharp
CreateBusinessAsync(
    Guid upvestClientId,
    Guid idempotencyKey,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    CreateBusinessBody body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `idempotencyKey` | `Guid` | Header, Required | A UUID to be used as an idempotency key.  This prevents a duplicate request from being replayed.<br>https://docs.upvest.co/documentation/concepts/api_concepts/idempotency |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`CreateBusinessBody`](../../doc/models/containers/create-business-body.md) | Body, Optional | This is a container for one-of cases. |

## Requires scope

### oauth-client-credentials

`businesses:admin`

## Response Type

**200**: Business created.

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type CreateBusinessResponse.

## Example Usage

```csharp
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
Guid idempotencyKey = new Guid("ccb07f42-4104-44ad-8e1f-c660bb7b269c");
CreateBusinessBody body = CreateBusinessBody.FromBusinessCompanyCreateRequest(
    new BusinessCompanyCreateRequest
    {
        ContactEmail = "acme.corporation@example.com",
        RegisteredAddress = new Address
        {
            AddressLine1 = "110",
            Postcode = "101110",
            Country = Country.De,
            City = "Berlin",
            AddressLine2 = "Schonhauser Allee",
            State = "BE",
        },
        TaxInformation = new TaxInformation
        {
            TaxCountryCode = TaxCountryCode.De,
            TaxIdentificationType = TaxIdentificationType.Steuernummer,
            TaxIdentificationNumber = "12345678901",
            IsResidentInMultipleTaxJurisdictions = false,
            IsSubjectToFatca = false,
        },
        BusinessType = "LIMITED_LIABILITY_COMPANY",
        Identification = new Identification8
        {
            CompanyName = "Acme Corporation",
            IncorporationDate = "2000-10-10",
            LegalDesignation = LegalDesignation.GesellschaftMitBeschraenkterHaftung,
            CommonReportingStandards = new CommonReportingStandards
            {
                IsPassiveNonFinancialEntity = false,
                IsFinancialInstitution = false,
            },
            DistrictCourt = "Berlin Charlottenburg",
            LegalEntityIdentifier = "213800K98H63P249S632",
        },
        TermsAndConditions = new TermsAndConditions4
        {
            ConsentDocumentId = new Guid("6a163d14-5b3d-48be-ac90-43314e96be71"),
            ConfirmedAt = DateTime.ParseExact("2024-10-24T14:14:22Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
        DataPrivacyAndSharingAgreement = new DataPrivacyAndSharingAgreement4
        {
            ConsentDocumentId = new Guid("fa2227c3-2b29-47e1-84da-996d09517edc"),
            ConfirmedAt = DateTime.ParseExact("2024-10-24T14:16:22Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                provider: CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
        },
        RegistrationNumber = "111221111",
    }
);

try
{
    ApiResponse<CreateBusinessResponse> result = await businessesApi.CreateBusinessAsync(
        upvestClientId,
        idempotencyKey,
        null,
        body
    );
    result.Data.Match<VoidType>(
        businessCompanyCreateResponse: businessCompanyCreateResponse =>
        {
            // TODO: handle businessCompanyCreateResponse here
            Console.WriteLine(businessCompanyCreateResponse);
            return null;
        },
        businessSoleTraderCreateResponse: businessSoleTraderCreateResponse =>
        {
            // TODO: handle businessSoleTraderCreateResponse here
            Console.WriteLine(businessSoleTraderCreateResponse);
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
  "id": "a9a72268-4f3c-4de2-abb9-a553a3bb7608",
  "created_at": "2024-10-24T14:14:22Z",
  "updated_at": "2024-10-24T14:14:22Z",
  "contact_email": "acme.corporation@example.com",
  "registered_address": {
    "address_line1": "110",
    "address_line2": "Schonhauser Allee",
    "postcode": "101110",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "registration_number": "111221111",
  "tax_information": {
    "tax_country_code": "DE",
    "tax_identification_type": "STEUERNUMMER",
    "tax_identification_number": "12345678901",
    "is_resident_in_multiple_tax_jurisdictions": false,
    "is_subject_to_fatca": false
  },
  "business_type": "LIMITED_LIABILITY_COMPANY",
  "identification": {
    "company_name": "Acme Corporation",
    "incorporation_date": "2000-10-10",
    "district_court": "Berlin Charlottenburg",
    "legal_designation": "GESELLSCHAFT_MIT_BESCHRAENKTER_HAFTUNG",
    "legal_entity_identifier": "213800K98H63P249S632",
    "common_reporting_standards": {
      "is_passive_non_financial_entity": false,
      "is_financial_institution": false
    }
  },
  "terms_and_conditions": {
    "consent_document_id": "6a163d14-5b3d-48be-ac90-43314e96be71",
    "confirmed_at": "2024-10-24T14:14:22Z"
  },
  "data_privacy_and_sharing_agreement": {
    "consent_document_id": "fa2227c3-2b29-47e1-84da-996d09517edc",
    "confirmed_at": "2024-10-24T14:16:22Z"
  },
  "status": "ACTIVE"
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


# Retrieve Business

Returns the business identified by `business_id`, including profile and status details

```csharp
RetrieveBusinessAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |

## Requires scope

### oauth-client-credentials

`businesses:admin`, `businesses:read`

## Response Type

**200**: OK

This method returns an [`ApiResponse`](../../doc/api-response.md) instance. The `Data` property of this instance returns the response data which is of type RetrieveBusinessResponse.

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
UpvestApiVersion? upvestApiVersion = UpvestApiVersion.Enum1;
try
{
    ApiResponse<RetrieveBusinessResponse> result = await businessesApi.RetrieveBusinessAsync(
        businessId,
        upvestClientId,
        upvestApiVersion
    );
    result.Data.Match<VoidType>(
        businessesResponse: businessesResponse =>
        {
            // TODO: handle businessesResponse here
            Console.WriteLine(businessesResponse);
            return null;
        },
        businessesResponse1: businessesResponse1 =>
        {
            // TODO: handle businessesResponse1 here
            Console.WriteLine(businessesResponse1);
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
  "id": "a9a72268-4f3c-4de2-abb9-a553a3bb7608",
  "created_at": "2024-10-24T14:14:22Z",
  "updated_at": "2024-10-24T14:14:22Z",
  "contact_email": "acme.corporation@example.com",
  "registered_address": {
    "address_line1": "110",
    "address_line2": "Schonhauser Allee",
    "postcode": "101110",
    "city": "Berlin",
    "state": "BE",
    "country": "DE"
  },
  "registration_number": "111221111",
  "tax_information": {
    "tax_country_code": "DE",
    "tax_identification_type": "STEUERNUMMER",
    "tax_identification_number": "12345678901",
    "is_resident_in_multiple_tax_jurisdictions": false,
    "is_subject_to_fatca": false
  },
  "business_type": "LIMITED_LIABILITY_COMPANY",
  "identification": {
    "company_name": "Acme Corporation",
    "incorporation_date": "2000-01-01",
    "district_court": "Berlin Charlottenburg",
    "legal_designation": "GESELLSCHAFT_MIT_BESCHRAENKTER_HAFTUNG",
    "legal_entity_identifier": "213800K98H63P249S632",
    "common_reporting_standards": {
      "is_passive_non_financial_entity": false,
      "is_financial_institution": false
    }
  },
  "terms_and_conditions": {
    "consent_document_id": "6a163d14-5b3d-48be-ac90-43314e96be71",
    "confirmed_at": "2024-10-24T14:14:22Z"
  },
  "data_privacy_and_sharing_agreement": {
    "consent_document_id": "fa2227c3-2b29-47e1-84da-996d09517edc",
    "confirmed_at": "2024-10-24T14:16:22Z"
  },
  "status": "ACTIVE"
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


# Business Data Change

Requests a data change for a business specified by ID.

```csharp
BusinessDataChangeAsync(
    Guid businessId,
    Guid upvestClientId,
    Models.UpvestApiVersion? upvestApiVersion = Models.UpvestApiVersion.Enum1,
    Models.BusinessCompanyUpdateRequest body = null)
```

## Authentication

This endpoint requires [oauth-client-credentials](../../doc/auth/oauth-2-client-credentials-grant.md)

## Parameters

| Parameter | Type | Tags | Description |
|  --- | --- | --- | --- |
| `businessId` | `Guid` | Template, Required | The unique identifier of the business. Universally Unique Identifier (UUID). |
| `upvestClientId` | `Guid` | Header, Required | Your client ID, issued by Upvest. Identifies the client making the request. Universally Unique Identifier (UUID). |
| `upvestApiVersion` | [`UpvestApiVersion?`](../../doc/models/upvest-api-version.md) | Header, Optional | Upvest API version (Note: Do not include quotation marks)<br><br>**Default**: `UpvestApiVersion.Enum_1` |
| `body` | [`BusinessCompanyUpdateRequest`](../../doc/models/business-company-update-request.md) | Body, Optional | - |

## Requires scope

### oauth-client-credentials

`businesses:admin`

## Response Type

**202**: The request has been successfully accepted and will be processed asynchronously.

`Task`

## Example Usage

```csharp
Guid businessId = new Guid("0000001e-0000-0000-0000-000000000000");
Guid upvestClientId = new Guid("363f3305-7ab0-4e82-a158-f9d382ad08b6");
BusinessCompanyUpdateRequest body = new BusinessCompanyUpdateRequest
{
    ContactEmail = "acme.corporation@example.com",
};

try
{
    await businessesApi.BusinessDataChangeAsync(
        businessId,
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

