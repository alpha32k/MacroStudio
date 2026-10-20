<#
.SYNOPSIS
Builds the Macro Studio installers: one .msi with Turkish screens and one with English screens.

.DESCRIPTION
1. Publishes the app as a self-contained win-x64 build (no .NET needed on the target computer).
2. Signs the app's own files when a code-signing certificate is given.
3. Builds the .msi files with WiX v5.
4. Signs the .msi files when a certificate is given.
The results land in installer\out.

.PARAMETER CertificateThumbprint
Thumbprint of a code-signing certificate in the current user's certificate store. Without it nothing is signed,
and Windows SmartScreen warns about an unknown publisher when the .msi is opened.

.EXAMPLE
.\installer\build.ps1
.\installer\build.ps1 -CertificateThumbprint 0123456789ABCDEF0123456789ABCDEF01234567
#>
param(
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $PSScriptRoot 'obj\publish'
$out = Join-Path $PSScriptRoot 'out'

function Find-SignTool {
    $found = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($null -eq $found) {
        throw 'signtool.exe was not found. Install the Windows SDK (signing tools) or build without -CertificateThumbprint.'
    }

    return $found.FullName
}

function Sign([string[]]$files) {
    & $script:signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $files
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $files" }
}

if ($CertificateThumbprint) {
    $script:signTool = Find-SignTool
}

Write-Host 'Publishing the app...'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish "$root\src\MacroStudio.App\MacroStudio.App.csproj" -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }

if ($CertificateThumbprint) {
    Write-Host 'Signing the app...'
    Sign (Get-ChildItem $publish -File | Where-Object { $_.Name -like 'MacroStudio*' } | ForEach-Object { $_.FullName })
}

Write-Host 'Building the installers...'
# Always from scratch: an incremental build does not notice a new version number and would keep the old .msi.
dotnet build "$PSScriptRoot\MacroStudio.Installer.wixproj" -c Release --no-incremental -p:AppPublishDir=$publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Building the installers failed.' }

[xml]$props = Get-Content "$root\Directory.Build.props"
$version = $props.Project.PropertyGroup.Version
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($culture in 'tr-TR', 'en-US') {
    $msi = Get-ChildItem "$PSScriptRoot\bin" -Recurse -Filter MacroStudio.msi | Where-Object { $_.Directory.Name -eq $culture } | Select-Object -First 1
    $target = Join-Path $out "MacroStudio-$version-$culture.msi"
    Copy-Item $msi.FullName $target -Force
    if ($CertificateThumbprint) { Sign $target }
    Write-Host "Ready: $target"
}
