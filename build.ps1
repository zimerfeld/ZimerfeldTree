#Requires -Version 5.1
<#
.SYNOPSIS
    Compila o plugin, incrementa a versao (major.minor.build) e gera o .nupkg.
    Executar como Administrador para tambem fazer o deploy em GitExtensions.
#>

$ErrorActionPreference = "Stop"

$nuspec  = "$PSScriptRoot\src\GitExtensions.ZimerfeldTree\GitExtensions.ZimerfeldTree.nuspec"
$csproj  = "$PSScriptRoot\src\GitExtensions.ZimerfeldTree\GitExtensions.ZimerfeldTree.csproj"
$outDir  = $PSScriptRoot

# -- 1. Ler versao atual do nuspec ---------------------------------------------
[xml]$spec    = Get-Content $nuspec -Encoding UTF8
$current      = $spec.package.metadata.version
$parts        = $current -split '\.'
if ($parts.Count -ne 3) {
    Write-Error "Versao '$current' nao esta no formato major.minor.build"
    exit 1
}
$major = [int]$parts[0]
$minor = [int]$parts[1]
$build = [int]$parts[2] + 1
$newVersion = "$major.$minor.$build"
Write-Host "Versao: $current  ->  $newVersion"

# -- 2. Atualizar nuspec -------------------------------------------------------
$spec.package.metadata.version = $newVersion
$spec.Save($nuspec)

# -- 3. Atualizar csproj -------------------------------------------------------
$csprojContent = Get-Content $csproj -Raw -Encoding UTF8
$csprojContent = $csprojContent -replace '<Version>[^<]+</Version>',         "<Version>$newVersion</Version>"
$csprojContent = $csprojContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$newVersion.0</AssemblyVersion>"
$csprojContent = $csprojContent -replace '<FileVersion>[^<]+</FileVersion>',   "<FileVersion>$newVersion.0</FileVersion>"
[System.IO.File]::WriteAllText($csproj, $csprojContent, [System.Text.Encoding]::UTF8)

# -- 4. Build ------------------------------------------------------------------
Write-Host "Compilando..."
dotnet build $csproj -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Error "Build falhou."; exit 1 }

# -- 5. Deploy (requer Admin) --------------------------------------------------
$pluginsDir = "C:\Program Files\GitExtensions\Plugins"
if (-not (Test-Path $pluginsDir)) {
    $pluginsDir = "C:\Program Files (x86)\GitExtensions\Plugins"
}
$dll = "$PSScriptRoot\src\GitExtensions.ZimerfeldTree\bin\Release\net9.0-windows\GitExtensions.Plugins.ZimerfeldTree.dll"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin -and (Test-Path $pluginsDir)) {
    Copy-Item $dll $pluginsDir -Force
    Write-Host "Plugin instalado em: $pluginsDir"
} else {
    Write-Warning "Sem permissao de Admin ou pasta nao encontrada -- deploy pulado."
    Write-Host "  Copie manualmente: $dll"
    Write-Host "  Para: $pluginsDir"
}

# Atualiza copia na pasta tools (usada pelo nupkg)
$toolsTarget = "$PSScriptRoot\tools\net9.0-windows"
if (-not (Test-Path $toolsTarget)) { New-Item -ItemType Directory $toolsTarget | Out-Null }
Copy-Item $dll $toolsTarget -Force

# -- 6. Pack -------------------------------------------------------------------
Write-Host "Gerando pacote $newVersion..."
nuget pack $nuspec -OutputDirectory $outDir
if ($LASTEXITCODE -ne 0) { Write-Error "nuget pack falhou."; exit 1 }

# Remove pacotes de versoes anteriores
Get-ChildItem "$outDir\GitExtensions.ZimerfeldTree.*.nupkg" |
    Where-Object { $_.Name -notlike "*$newVersion*" } |
    Remove-Item -Force

Write-Host ""
Write-Host "Concluido: GitExtensions.ZimerfeldTree.$newVersion.nupkg"
