
# Method

Method used for AML-compliant KYC process

* VIDEO_ID - Video identification
* IN_PERSON_ID - In-person identification at the post office or the client's outlet
* ELECTRONIC_ID - Advanced electronic identification methods (namely German eID)
* LIVENESS_PHOTO_ID - Photos and security features of the identification document in combination with a video-based liveness check (residence country must not be Germany in this case)
* QUALIFIED_ELECTRONIC_SIGNATURE_WITH_TX - Qualified electronic signature accompanied by a bank transaction for verification
* TWO_PLUS_TWO_VERIFICATION - A method of verifying identity by matching at least two personal details from two separate sources (for GB residence users only)
* ID_COLLECTION - The identification document of the user is collected but has not undergone verification

## Enumeration

`Method`

## Fields

| Name |
|  --- |
| `VideoId` |
| `InPersonId` |
| `ElectronicId` |
| `LivenessPhotoId` |
| `QualifiedElectronicSignatureWithTx` |
| `TwoPlusTwoVerification` |
| `IdCollection` |

## Example

```csharp
using UpvestInvestmentApi.Standard.Models;

Method method = Method.VideoId;
```

