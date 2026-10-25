param(
    [Parameter(Mandatory)][ValidateSet('setup', 'api', 'verify', 'stop-db')][string]$Action,
    [ValidateRange(1024, 65535)][int]$Port = 5080
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$onboardflowRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $onboardflowRoot
$onboardflowDotnet = Join-Path $onboardflowRoot '.local/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $onboardflowDotnet)) { $onboardflowDotnet = (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path $onboardflowDotnet -Parent
$env:PATH = "$env:DOTNET_ROOT$([IO.Path]::PathSeparator)$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

if ($Action -eq 'stop-db') { docker compose stop db; exit }

if ($Action -eq 'setup' -and !(Test-Path -LiteralPath .local/database.env)) {
    New-Item -ItemType Directory -Force -Path .local | Out-Null
    $onboardflowPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    "POSTGRES_USER=onboardflow`nPOSTGRES_DB=onboardflow_dev`nPOSTGRES_PASSWORD=$onboardflowPassword" |
        Set-Content -LiteralPath .local/database.env
}
if (!(Test-Path -LiteralPath .local/database.env)) { throw 'Run npm run setup first.' }
$onboardflowPassword = ((Get-Content -LiteralPath .local/database.env |
    Where-Object { $_.StartsWith('POSTGRES_PASSWORD=') }) -split '=', 2)[1]
$onboardflowConnection = "Host=127.0.0.1;Port=54329;Database=onboardflow_dev;Username=onboardflow;Password=$onboardflowPassword"
if (!$env:ConnectionStrings__OnboardFlow) { $env:ConnectionStrings__OnboardFlow = $onboardflowConnection }
$env:ASPNETCORE_ENVIRONMENT = 'Development'

switch ($Action) {
    'setup' {
        docker compose up -d --wait db
        & $onboardflowDotnet restore OnboardFlow.slnx --locked-mode
        & $onboardflowDotnet tool restore
        & $onboardflowDotnet ef database update --project src/OnboardFlow.Api
        npx playwright install chromium firefox webkit
    }
    'api' {
        & $onboardflowDotnet run --project src/OnboardFlow.Api --no-launch-profile --urls "http://127.0.0.1:$Port"
    }
    'verify' {
        $env:ONBOARDFLOW_LIVE_AI = '0'
        $env:Intake__Provider = 'Ollama'
        Remove-Item Env:Intake__SpendConnectionString -ErrorAction SilentlyContinue
        npm run build
        & $onboardflowDotnet build OnboardFlow.slnx --no-restore
        $env:ONBOARDFLOW_TEST_CONNECTION = $onboardflowConnection
        & $onboardflowDotnet test OnboardFlow.slnx --no-build --logger 'trx;LogFileName=foundation.trx' --results-directory .local/reports

        # This test owns a disposable PostgreSQL database and injects a model double
        # only into its Kestrel host. No test provider exists in the shipped app.
        $env:ONBOARDFLOW_BROWSER_TEST = '1'
        try {
            & $onboardflowDotnet test OnboardFlow.slnx --no-build --filter 'Category=BrowserCI' --logger 'trx;LogFileName=browser.trx' --results-directory .local/reports
        }
        finally {
            Remove-Item Env:ONBOARDFLOW_BROWSER_TEST -ErrorAction SilentlyContinue
        }
    }
}
