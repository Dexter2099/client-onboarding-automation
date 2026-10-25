param(
    [ValidateSet('setup', 'api', 'test', 'evaluate')][string]$Action = 'api',
    [ValidateSet('Ollama', 'Cloudflare', 'OpenAI')][string]$Provider = 'Ollama',
    [string]$EvaluationFilter = ''
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$intakeRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $intakeRoot
$intakeDotnet = Join-Path $intakeRoot '.local/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $intakeDotnet)) { $intakeDotnet = (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path $intakeDotnet -Parent
$env:PATH = "$env:DOTNET_ROOT$([IO.Path]::PathSeparator)$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if (!(Test-Path -LiteralPath .local/database.env)) {
    if ($Action -ne 'setup') { throw 'Run scripts/intake-local.ps1 setup first.' }
    New-Item -ItemType Directory -Force -Path .local | Out-Null
    $intakeNewPassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
    @('POSTGRES_USER=onboardflow', 'POSTGRES_DB=onboardflow_dev', "POSTGRES_PASSWORD=$intakeNewPassword") |
        Set-Content -LiteralPath .local/database.env
}
$intakePassword = ((Get-Content -LiteralPath .local/database.env |
    Where-Object { $_.StartsWith('POSTGRES_PASSWORD=') }) -split '=', 2)[1]
# Deliberately force only this project's loopback database, regardless of inherited hosting variables.
$env:ConnectionStrings__OnboardFlow = "Host=127.0.0.1;Port=54329;Database=onboardflow_dev;Username=onboardflow;Password=$intakePassword"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Remove-Item Env:Intake__SpendConnectionString -ErrorAction SilentlyContinue
if ($Action -in @('api', 'evaluate')) { $env:Intake__SpendConnectionString = $env:ConnectionStrings__OnboardFlow }
$env:Intake__Provider = $Provider
if ($Provider -eq 'OpenAI' -and $Action -in @('api', 'evaluate') -and (Test-Path -LiteralPath .local/production-cutover.json)) {
    throw 'Paid local inference is paused after production cutover. Use Ollama locally; do not split this project across separate spending ledgers.'
}
if ($Provider -eq 'Cloudflare' -and $Action -in @('api', 'evaluate')) {
    # Server-side only. Do not echo secrets or import unrelated environment settings.
    if (Test-Path -LiteralPath .local/cloudflare.env) {
        foreach ($intakeSetting in Get-Content -LiteralPath .local/cloudflare.env) {
            if ($intakeSetting -match '^(Intake__Cloudflare__(AccountId|ApiToken|Model))=(.+)$') {
                [Environment]::SetEnvironmentVariable($Matches[1], $Matches[3], 'Process')
            }
        }
    }
    if (!$env:Intake__Cloudflare__AccountId -or !$env:Intake__Cloudflare__ApiToken) {
        throw 'Cloudflare credentials are missing. See the hosted-AI checkpoint in HARNESS.md. Never paste the token in chat.'
    }
}
if ($Provider -eq 'OpenAI' -and $Action -in @('api', 'evaluate')) {
    if (Test-Path -LiteralPath .local/openai.env) {
        foreach ($intakeSetting in Get-Content -LiteralPath .local/openai.env) {
            if ($intakeSetting -match '^(Intake__OpenAI__(ApiKey|ProjectId|BudgetVerified))=(.+)$') {
                [Environment]::SetEnvironmentVariable($Matches[1], $Matches[3], 'Process')
            }
        }
    }
    if (!$env:Intake__OpenAI__ApiKey -or !$env:Intake__OpenAI__ProjectId -or $env:Intake__OpenAI__BudgetVerified -ne 'true') {
        throw 'OpenAI credentials or verified project spending limit are missing. See HARNESS.md. Never paste the key in chat.'
    }
}
switch ($Action) {
    'setup' {
        docker compose up -d --wait db
        & $intakeDotnet tool restore
        & $intakeDotnet ef database update --project src/OnboardFlow.Api
    }
    'api' { & $intakeDotnet run --project src/OnboardFlow.Api -p:UseAppHost=false --no-launch-profile --urls http://127.0.0.1:5080 }
    'test' {
        $env:ONBOARDFLOW_LIVE_AI = '0'
        $env:ONBOARDFLOW_TEST_CONNECTION = $env:ConnectionStrings__OnboardFlow
        & $intakeDotnet test OnboardFlow.slnx --logger 'trx;LogFileName=intake.trx' --results-directory .local/reports
    }
    'evaluate' {
        $env:ONBOARDFLOW_TEST_CONNECTION = $env:ConnectionStrings__OnboardFlow
        $env:ONBOARDFLOW_LIVE_AI = '1'
        $intakeEvaluation = $Provider.ToLowerInvariant() + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $env:ONBOARDFLOW_EVALUATION_OUTPUT = Join-Path $intakeRoot ".local/intake-evaluation/$intakeEvaluation"
        Write-Output "Evaluation evidence: $env:ONBOARDFLOW_EVALUATION_OUTPUT"
        $intakeFilter = 'Category=LiveAI'
        if ($EvaluationFilter) { $intakeFilter += '&(' + $EvaluationFilter + ')' }
        & $intakeDotnet test OnboardFlow.slnx --filter $intakeFilter --logger 'trx;LogFileName=intake-live.trx' --results-directory .local/reports
    }
}
