param()

$skillRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")
$projectPath = Join-Path $repoRoot "TricksterMap.Cli\TricksterMap.Cli.csproj"
$publishDir = Join-Path $skillRoot "assets\bin"

if (-not (Test-Path $projectPath)) {
    Write-Error "Cannot find TricksterMap.Cli project at $projectPath"
    exit 1
}

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

dotnet publish $projectPath -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Get-ChildItem -File $publishDir | Select-Object Name, Length
