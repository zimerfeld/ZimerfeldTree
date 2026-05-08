#Requires -Version 5.1
<#
.SYNOPSIS
    Instala o plugin ZimerfeldTree no GitExtensions.

.DESCRIPTION
    Pode ser executado de duas formas:

      Opcao A - standalone (Execute diretamente):
          cd C:\NUGET\ZimerfeldTree\tools
          .\install.ps1

      Opcao B - via NuGet Package Manager Console (Visual Studio):
          Install-Package GitExtensions.ZimerfeldTree -Source C:\NUGET
          (O NuGet invoca este script automaticamente passando $installPath, $toolsPath, etc.)

.NOTES
    Requer permissao de Administrador para copiar para
    C:\Program Files\GitExtensions\Plugins\.
#>

param(
    # Provided automatically by NuGet PMC; ignored when run standalone
    $installPath,
    $toolsPath,
    $package,
    $project
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -- Locate the plugin DLL ----------------------------------------------------

# When run by NuGet PMC, $toolsPath is set to the package's tools\ folder.
# When run standalone, resolve relative to this script's location.
if ($toolsPath) {
    $dllDir = Join-Path $toolsPath "net9.0-windows"
} else {
    $dllDir = Join-Path $PSScriptRoot "net9.0-windows"
}

$dllName = "GitExtensions.Plugins.ZimerfeldTree.dll"
$dll     = Join-Path $dllDir $dllName

if (-not (Test-Path $dll)) {
    Write-Error ("DLL nao encontrada: $dll`n`nExecute build.ps1 primeiro para compilar o plugin:`n  pwsh C:\NUGET\ZimerfeldTree\build.ps1")
    exit 1
}

# -- Locate GitExtensions Plugins folder --------------------------------------

$candidates = @(
    "C:\Program Files\GitExtensions\Plugins",
    "C:\Program Files (x86)\GitExtensions\Plugins"
)

$pluginsDir = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $pluginsDir) {
    Write-Warning ("Pasta de plugins do GitExtensions nao encontrada nos caminhos padrao.`n`nCopie manualmente o arquivo:`n  $dll`npara a pasta Plugins\ da sua instalacao do GitExtensions.")
    exit 0
}

# -- Check administrator rights -----------------------------------------------

$currentUser     = [Security.Principal.WindowsIdentity]::GetCurrent()
$principalObject = New-Object Security.Principal.WindowsPrincipal($currentUser)
$isAdmin         = $principalObject.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Warning ("Este script precisa de permissao de Administrador para instalar em:`n  $pluginsDir`n`nRe-execute o PowerShell como Administrador e repita:`n  cd $PSScriptRoot`n  .\install.ps1")
    exit 1
}

# -- Copy DLL -----------------------------------------------------------------

$dest = Join-Path $pluginsDir $dllName

try {
    Copy-Item -Path $dll -Destination $dest -Force
    Write-Host ""
    Write-Host "Plugin instalado com sucesso em:" -ForegroundColor Green
    Write-Host "  $dest" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Reinicie o GitExtensions e acesse Plugins -> ZimerfeldTree."
}
catch {
    Write-Error "Falha ao copiar DLL: $_"
    exit 1
}
