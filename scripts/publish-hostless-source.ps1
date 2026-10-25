param([switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Set-Location -LiteralPath (Split-Path $PSScriptRoot -Parent)
$branch = 'deploy/hostless'
$repository = 'https://github.com/Dexter2099/client-onboarding-automation.git'
$manifest = Get-Content -Raw -LiteralPath .local/release/release-manifest.json | ConvertFrom-Json
if ($manifest.revision -notmatch '^[a-f0-9]{40}$') { throw 'Invalid release revision.' }
if ($manifest.repository -ne $repository.Replace('.git', '')) { throw 'Unexpected release repository.' }
if ($manifest.file -ne "onboardflow-$($manifest.revision).zip") { throw 'Unexpected release filename.' }
$archive = Join-Path '.local/release' $manifest.file
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.sha256) {
    throw 'The verified source package checksum does not match.'
}
if (!$VerifyOnly) {
    if ($env:GITHUB_REPOSITORY -ne 'Dexter2099/client-onboarding-automation' -or
        $env:GITHUB_REF -ne 'refs/heads/main' -or $env:GITHUB_SHA -ne $manifest.revision -or
        !$env:GITHUB_TOKEN) { throw 'Only the verified main workflow can publish a release.' }
}
$output = [IO.Path]::GetFullPath('.local/hostless-publish')
if (Test-Path -LiteralPath $output) { throw 'Use a fresh Hostless staging directory.' }
Expand-Archive -LiteralPath $archive -DestinationPath $output
$marker = Get-Content -Raw -LiteralPath (Join-Path $output 'public/release.json') | ConvertFrom-Json
if ($marker.revision -ne $manifest.revision -or $marker.repository -ne $manifest.repository) {
    throw 'The packaged release marker does not match.'
}
git -C $output init --quiet
git -C $output remote add origin $repository
git -C $output config user.name 'github-actions[bot]'
git -C $output config user.email '41898282+github-actions[bot]@users.noreply.github.com'
git -C $output symbolic-ref HEAD "refs/heads/$branch"

# Retain an ordinary fast-forward history. Never force-push or publish the working tree.
$PSNativeCommandUseErrorActionPreference = $false
$remote = git -C $output ls-remote --exit-code --heads origin "refs/heads/$branch"
$remoteExit = $LASTEXITCODE
$PSNativeCommandUseErrorActionPreference = $true
if ($remoteExit -eq 0) {
    git -C $output fetch --quiet --depth=1 origin "refs/heads/$branch"
    git -C $output update-ref "refs/heads/$branch" FETCH_HEAD
    git -C $output read-tree HEAD
}
elseif ($remoteExit -ne 2) { throw 'Could not inspect the deployment branch.' }
# The release branch does not need workflow definitions or permission to write them.
git -C $output add --all -- . ':!.github'
git -C $output commit --quiet --allow-empty -m "Release $($manifest.revision)"
if ($VerifyOnly) {
    Write-Output "Verified export and deployment commit for $($manifest.revision); nothing pushed."
    exit
}

# Git asks this helper for its short-lived Actions token. Never embed it in a URL or log.
$askpass = [IO.Path]::GetFullPath('.local/hostless-git-askpass.sh')
@'
#!/bin/sh
case "$1" in
  *Username*) printf '%s\n' x-access-token ;;
  *Password*) printf '%s\n' "$GITHUB_TOKEN" ;;
esac
'@ | Set-Content -LiteralPath $askpass -Encoding utf8NoBOM
chmod 700 $askpass
$env:GIT_ASKPASS = $askpass
$env:GIT_TERMINAL_PROMPT = '0'
try { git -C $output push --quiet origin "HEAD:refs/heads/$branch" }
finally {
    Remove-Item Env:GIT_ASKPASS -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $askpass
}
Write-Output "Published verified revision $($manifest.revision) to $branch."
