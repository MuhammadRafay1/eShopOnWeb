
# User Check List Response

Response containing the list of all checks conducted for a user.

## Structure

`UserCheckListResponse`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Data` | [`List<UserCheckListResponseData>`](../../doc/models/containers/user-check-list-response-data.md) | Required | This is List of a container for one-of cases. |

## Example

```csharp
using System.Collections.Generic;
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;
using UpvestInvestmentApi.Standard.Models.Containers;
using UpvestInvestmentApi.Standard.Utilities;

UserCheckListResponse userCheckListResponse = new UserCheckListResponse
{
    Data = new List<UserCheckListResponseData>
    {
        UserCheckListResponseData.FromUserCheckKnowYourCustomer(
            new UserCheckKnowYourCustomer
            {
                Id = new Guid("00001800-0000-0000-0000-000000000000"),
                UserId = new Guid("00001f10-0000-0000-0000-000000000000"),
                Type = "type6",
                CheckConfirmedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
                    provider: CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind),
                DataDownloadLink = "data_download_link6",
                DocumentType = DocumentType3.ResidencePermit,
                Status = Status6.Passed,
                Provider = "provider4",
                Method = Method.TwoPlusTwoVerification,
                DocumentExpirationDate = DateTime.Parse("2016-03-13"),
                Nationality = "nationality2",
                ConfirmedAddress = new Address
                {
                    AddressLine1 = "address_line16",
                    Postcode = "postcode6",
                    Country = Country.Tv,
                    City = "city2",
                    AddressLine2 = "address_line24",
                    State = "state8",
                    ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
                },
                KycUpdate = false,
                ["exampleAdditionalProperty"] = ApiHelper.JsonDeserialize<object>("{\"key1\":\"val1\",\"key2\":\"val2\"}"),
            }
        ),
    },
};
```

