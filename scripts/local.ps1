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
        & $onboardflowDotnet restore OnboardFlow.slnx
        & $onboardflowDotnet tool restore
        & $onboardflowDotnet ef database update --project src/OnboardFlow.Api
        npx playwright install chromium
    }
    'api' {
        & $onboardflowDotnet run --project src/OnboardFlow.Api --no-launch-profile --urls "http://127.0.0.1:$Port"
    }
    'verify' {
        npm run build
        & $onboardflowDotnet build OnboardFlow.slnx --no-restore
        $env:ONBOARDFLOW_TEST_CONNECTION = $onboardflowConnection
        & $onboardflowDotnet test OnboardFlow.slnx --no-build --logger 'trx;LogFileName=foundation.trx' --results-directory .local/reports

        # Browser checks own a separate database. Never migrate/reset the development database here.
        $onboardflowBrowserDatabase = 'onboardflow_browser_test_' + [Guid]::NewGuid().ToString('N')
        docker compose exec -T db psql -U onboardflow -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $onboardflowBrowserDatabase"
        try {
            $env:ConnectionStrings__OnboardFlow = $onboardflowConnection.Replace('Database=onboardflow_dev;', "Database=$onboardflowBrowserDatabase;")
            & $onboardflowDotnet ef database update --project src/OnboardFlow.Api --no-build
            npx playwright test
        }
        finally {
            $env:ConnectionStrings__OnboardFlow = $onboardflowConnection
            docker compose exec -T db psql -U onboardflow -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE $onboardflowBrowserDatabase WITH (FORCE)"
        }
    }
}
