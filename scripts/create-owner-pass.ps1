param(
    [ValidateRange(1, 90)][int]$Days = 90,
    [string]$SigningKeyPath = '.local/owner-access/signing-key.dpapi',
    [string]$OutputPath = '.local/owner-access/owner-pass.dpapi'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$ownerOutput = [IO.Path]::GetFullPath($OutputPath)
$ownerPrivateRoot = [IO.Path]::GetFullPath('.local') + [IO.Path]::DirectorySeparatorChar
if (!$ownerOutput.StartsWith($ownerPrivateRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Owner passes must remain in this project''s ignored .local directory.'
}
$ownerSettings = Get-Content -Raw src/OnboardFlow.Api/appsettings.json | ConvertFrom-Json
$ownerKeyBytes = [Security.Cryptography.ProtectedData]::Unprotect(
    [Convert]::FromBase64String([IO.File]::ReadAllText([IO.Path]::GetFullPath($SigningKeyPath))),
    $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
$ownerSigner = [Security.Cryptography.ECDsa]::Create()
try {
    $ownerRead = 0
    $ownerSigner.ImportPkcs8PrivateKey([Convert]::FromBase64String([Text.Encoding]::UTF8.GetString($ownerKeyBytes)), [ref]$ownerRead)
    if ([Convert]::ToBase64String($ownerSigner.ExportSubjectPublicKeyInfo()) -ne $ownerSettings.OwnerAccess.PublicKey) {
        throw 'The recovery signing key does not match the application''s public verifier.'
    }
    $ownerExpires = [DateTimeOffset]::UtcNow.AddDays($Days).ToUnixTimeSeconds().ToString([Globalization.CultureInfo]::InvariantCulture)
    $ownerNonce = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)).ToLowerInvariant()
    $ownerCanonical = @('onboardflow-owner-runs-v1', $ownerSettings.VerifiedEdge.PublicOrigin, $ownerExpires, $ownerNonce) -join "`n"
    $ownerSignature = $ownerSigner.SignData([Text.Encoding]::UTF8.GetBytes($ownerCanonical),
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
    $ownerEncodedSignature = [Convert]::ToBase64String($ownerSignature).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    $ownerPassBytes = [Text.Encoding]::UTF8.GetBytes("v1.$ownerExpires.$ownerNonce.$ownerEncodedSignature")
    try {
        $ownerProtected = [Security.Cryptography.ProtectedData]::Protect($ownerPassBytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($ownerOutput)) | Out-Null
        [IO.File]::WriteAllText($ownerOutput, [Convert]::ToBase64String($ownerProtected))
    }
    finally { [Array]::Clear($ownerPassBytes) }
    Write-Output "Owner pass saved encrypted for this Windows account at $ownerOutput. Valid for $Days days."
    Write-Output "Activate privately at $($ownerSettings.VerifiedEdge.PublicOrigin)/api/owner-access. Never paste the pass into chat or a URL."
}
finally { [Array]::Clear($ownerKeyBytes); $ownerSigner.Dispose() }
