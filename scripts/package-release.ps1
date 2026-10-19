param(
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$Revision,
    [string]$OutputDirectory = '.local/release'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
if ((git rev-parse HEAD).Trim() -ne $Revision) { throw 'Package the checked-out revision only.' }

$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh release output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$stage = Join-Path $output source
$archive = Join-Path $output git-source.zip
# Export committed files only: never credentials, ignored state or local build output.
git archive --format=zip "--output=$archive" $Revision
Expand-Archive -LiteralPath $archive -DestinationPath $stage
$marker = [ordered]@{
    revision = $Revision
    repository = 'https://github.com/Dexter2099/client-onboarding-automation'
}
$marker | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage public/release.json) -Encoding utf8NoBOM
$filename = "onboardflow-$Revision.zip"
$bundle = Join-Path $output $filename
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $bundle)
$manifest = [ordered]@{
    revision = $Revision
    repository = $marker.repository
    file = $filename
    sha256 = (Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output release-manifest.json) -Encoding utf8NoBOM
Remove-Item -LiteralPath $archive
Write-Output ("Prepared revision {0}, SHA-256 {1}" -f $Revision, $manifest.sha256)
