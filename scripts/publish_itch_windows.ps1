param(
    [Parameter(Mandatory = $true)]
    [string]$ItchUser,

    [Parameter(Mandatory = $true)]
    [string]$GameSlug,

    [string]$Version = "0.1.0",
    [string]$Channel = "windows-demo",
    [string]$BuildDirectory = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
    $BuildDirectory = Join-Path $projectRoot "Builds\Windows\Elarion-Relics-of-the-Forgotten-Windows-x64"
}

$butler = Get-Command butler -ErrorAction SilentlyContinue
if ($null -eq $butler) {
    throw "Butler nao encontrado no PATH. Instale por https://itch.io/docs/butler/installing.html"
}

if (-not (Test-Path -LiteralPath $BuildDirectory -PathType Container)) {
    throw "Build nao encontrada em: $BuildDirectory"
}

$executable = Get-ChildItem -LiteralPath $BuildDirectory -Filter *.exe | Select-Object -First 1
if ($null -eq $executable) {
    throw "Nenhum executavel Windows foi encontrado na build."
}

$target = "$ItchUser/$GameSlug`:$Channel"
$butlerPath = $butler.Source
Write-Host "Publicando $BuildDirectory em $target (versao $Version)..."
& $butlerPath push $BuildDirectory $target --userversion $Version

if ($LASTEXITCODE -ne 0) {
    throw "O butler retornou o codigo $LASTEXITCODE."
}

Write-Host "Publicacao concluida em $target."
