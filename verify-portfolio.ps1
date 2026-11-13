<#
    Drives the managed-investment-portfolio endpoints on the PublicApi host end to end.

    Prerequisites:
      1. Upvest credentials loaded into PublicApi user-secrets (keys Upvest:ClientId, Upvest:ClientSecret,
         Upvest:SigningKeyId, Upvest:SigningKeyPath, Upvest:SigningKeyPassphrase, Upvest:BaseUrl).
      2. The PublicApi host running in Development with the in-memory DB, e.g.:
           $env:DOTNET_ROLL_FORWARD='Major'; $env:ASPNETCORE_ENVIRONMENT='Development'
           $env:UseOnlyInMemoryDatabase='true'
           $env:ASPNETCORE_URLS='https://localhost:42003;http://localhost:42004'
           dotnet run --project src/PublicApi/PublicApi.csproj --no-launch-profile

    Usage:  pwsh ./verify-portfolio.ps1
#>

$ErrorActionPreference = 'Stop'
$base = 'https://localhost:42003'

# Trust the dev cert for this session (PowerShell 7+).
function Invoke-Api {
    param([string]$Method, [string]$Path, [string]$Token, $Body)
    $headers = @{ Authorization = "Bearer $Token" }
    $args = @{ Method = $Method; Uri = "$base$Path"; Headers = $headers; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($Body) { $args.Body = ($Body | ConvertTo-Json -Depth 8); $args.ContentType = 'application/json' }
    $resp = Invoke-WebRequest @args
    Write-Host "  [$($resp.StatusCode)] $Method $Path" -ForegroundColor Cyan
    if ($resp.Content) { Write-Host "    $($resp.Content)" }
    return $resp
}

function Get-Token {
    param([string]$User)
    $r = Invoke-WebRequest -Method Post -Uri "$base/api/authenticate" -SkipCertificateCheck `
        -ContentType 'application/json' -Body (@{ username = $User; password = 'Pass@word1' } | ConvertTo-Json)
    return ($r.Content | ConvertFrom-Json).token
}

Write-Host "Authenticating..." -ForegroundColor Yellow
$user  = Get-Token 'demouser@microsoft.com'
$admin = Get-Token 'admin@microsoft.com'

Write-Host "`n== Flow 2: operator defines a model ==" -ForegroundColor Yellow
Invoke-Api PUT '/api/portfolio/models/balanced' $admin @{ name='Balanced'; funds=@(
    @{ instrumentId='IE00B4L5Y983'; weight=60 }, @{ instrumentId='IE00B3RBWM25'; weight=40 }) } | Out-Null
Invoke-Api GET '/api/portfolio/models' $user | Out-Null

Write-Host "`n== Flow 1: shopper signs up as an investor (EEA nationality -> real Upvest user) ==" -ForegroundColor Yellow
Invoke-Api POST '/api/portfolio/investor' $user @{
    firstName='Max'; lastName='Mustermann'; email='max@example.de'; birthDate='1988-04-12';
    birthCity='Berlin'; birthCountry='DE'; nationality='DE';
    address=@{ line1='Alexanderplatz 1'; postcode='10178'; city='Berlin'; country='DE' };
    phoneNumber='491701234567'; taxId='12345678901'; taxCountry='DE';
    identityDocument=@{ type='ID_CARD'; number='T22000129'; issuingCountry='DE'; expiryDate='2031-03-15' } } | Out-Null
Invoke-Api GET '/api/portfolio/investor' $user | Out-Null

Write-Host "`n== Flow 2: investor chooses a model ==" -ForegroundColor Yellow
Invoke-Api PUT '/api/portfolio/investor/model' $user @{ modelId='balanced' } | Out-Null

Write-Host "`n== Flow 3: pay in (refused while pending, per spec) ==" -ForegroundColor Yellow
Invoke-Api POST '/api/portfolio/payments' $user @{ amount=250.00 } | Out-Null
Invoke-Api GET '/api/portfolio/payments' $user | Out-Null

Write-Host "`n== Flow 4: operator realigns the model ==" -ForegroundColor Yellow
$r = Invoke-Api POST '/api/portfolio/models/balanced/realignments' $admin
$rid = ($r.Content | ConvertFrom-Json).realignmentId
Invoke-Api GET "/api/portfolio/realignments/$rid" $user | Out-Null

Write-Host "`n== Flow 5: the investor's portfolio ==" -ForegroundColor Yellow
Invoke-Api GET '/api/portfolio' $user | Out-Null

Write-Host "`n== Negative checks ==" -ForegroundColor Yellow
Invoke-Api PUT '/api/portfolio/models/x' $user @{ name='x'; funds=@(@{ instrumentId='IE00B4L5Y983'; weight=100 }) } | Out-Null  # 403 (non-admin)
Invoke-Api POST '/api/portfolio/investor' $admin @{
    firstName='John'; lastName='Bull'; email='john@example.co.uk'; birthDate='1980-01-01';
    birthCity='London'; birthCountry='GB'; nationality='GB';
    address=@{ line1='10 Downing St'; postcode='SW1A2AA'; city='London'; country='GB' };
    phoneNumber='441234567890'; taxId='AB123456C'; taxCountry='GB';
    identityDocument=@{ type='PASSPORT'; number='P1234567'; issuingCountry='GB'; expiryDate='2030-06-01' } } | Out-Null  # status: rejected

Write-Host "`nDone." -ForegroundColor Green
