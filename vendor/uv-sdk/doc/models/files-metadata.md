
# Files Metadata

## Structure

`FilesMetadata`

## Fields

| Name | Type | Tags | Description |
|  --- | --- | --- | --- |
| `Id` | `Guid` | Required | Files metadata identifier. |
| `CreatedAt` | `DateTime` | Required | Date and time when the resource was created. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `UpdatedAt` | `DateTime` | Required | Date and time when the resource was last updated. [RFC 3339-5](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), [ISO8601 UTC](https://www.iso.org/iso-8601-date-and-time-format.html) |
| `SignedUrl` | `string` | Required | Files download URL.<br><br>**Constraints**: *Maximum Length*: `1000` |
| `FileName` | `string` | Required | Name of the file.<br><br>**Constraints**: *Pattern*: `^[a-z0-9-_\.]{1,32}$` |
| `ContentLength` | `int` | Required | Length of the file |
| `Checksum` | `string` | Required | CRC32 of the files content<br><br>**Constraints**: *Pattern*: `^[a-f0-9]{64}$` |

## Example

```csharp
using System.Globalization;
using UpvestInvestmentApi.Standard.Models;

FilesMetadata filesMetadata = new FilesMetadata
{
    Id = new Guid("000011fa-0000-0000-0000-000000000000"),
    CreatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    UpdatedAt = DateTime.ParseExact("2016-03-13T12:52:32.123Z", "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK",
        provider: CultureInfo.InvariantCulture,
        DateTimeStyles.RoundtripKind),
    SignedUrl = "signed_url6",
    FileName = "file_name6",
    ContentLength = 160,
    Checksum = "checksum4",
};
```

