param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArgs
)

$skillRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $skillRoot "assets\bin"
$cliDllPath = Join-Path $publishDir "TricksterMap.Cli.dll"

if (-not (Test-Path $cliDllPath)) {
    Write-Error "Cannot find bundled TricksterMap CLI at $cliDllPath"
    exit 1
}

dotnet $cliDllPath @CliArgs
exit $LASTEXITCODE
