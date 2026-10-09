<#
Builds AmqTool (Release) and packages it for a GitHub release:

    powershell -ExecutionPolicy Bypass -File build\build-release.ps1

Output in dist\:
    AmqTool\                         the app files, exactly what gets installed
    AmqTool-v<version>.zip           portable zip. Help > Check for updates downloads this one, so attach it to every release
    AmqTool-Setup-v<version>.exe     installer for new users (needs Inno Setup 6: winget install JRSoftware.InnoSetup)

The version comes from AmqTool\Properties\AssemblyInfo.cs; tag the GitHub release v<version>.
#>
param(
    [switch]$NoInstaller
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'AmqTool.sln'
$dist = Join-Path $root 'dist'

# --- MSBuild from Visual Studio / Build Tools
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Visual Studio or the VS Build Tools are needed (vswhere.exe not found)' }
$msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found' }

# packages\ is gitignored, so a fresh clone has to restore it first (packages.config style project)
if (-not (Test-Path (Join-Path $root 'packages\Newtonsoft.Json.12.0.3'))) {
    & $msbuild $sln -t:restore -p:RestorePackagesConfig=true -nologo -v:minimal
    if ($LASTEXITCODE) { throw 'NuGet restore failed' }
}

# Release|Any CPU builds as 32-bit preferred, which is required because lib\mpv-1.dll is 32-bit. Don't use the x64 configs.
& $msbuild $sln -p:Configuration=Release '-p:Platform=Any CPU' -m -nologo -v:minimal
if ($LASTEXITCODE) { throw 'Build failed' }

$bin = Join-Path $root 'AmqTool\bin\Release'
$version = (Get-Item (Join-Path $bin 'AmqTool.exe')).VersionInfo.ProductVersion

# --- Stage only what the app needs. Never ship config.json / jobs.json / private_config.json / volume_jobs.json:
# they're per user, get created on first start, and the updater relies on them not being in the zip.
$stage = Join-Path $dist 'AmqTool'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'lib') | Out-Null
$files = 'AmqTool.exe', 'AmqTool.exe.config', 'Mpv.Net.Wpf.dll', 'Newtonsoft.Json.dll',
         'Microsoft.WindowsAPICodePack.dll', 'Microsoft.WindowsAPICodePack.Shell.dll', 'Microsoft.WindowsAPICodePack.ShellExtensions.dll',
         'config.template', 'private_config.template', 'lib\mpv-1.dll', 'lib\ytdl_hook.lua'
foreach ($f in $files) { Copy-Item (Join-Path $bin $f) (Join-Path $stage $f) }

# --- Portable zip (contains an AmqTool\ folder). Entries are added one by one because ZipFile.CreateFromDirectory
# writes backslash paths when run from Windows PowerShell 5.1, which isn't valid zip
$zip = Join-Path $dist "AmqTool-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = 'AmqTool/' + $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $archive.Dispose()
}

# --- Installer
$setup = $null
if (-not $NoInstaller) {
    $iscc = @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
              (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
              (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($iscc) {
        & $iscc /Q "/DAppVersion=$version" "/DSourceDir=$stage" "/O$dist" (Join-Path $root 'installer\AmqTool.iss')
        if ($LASTEXITCODE) { throw 'Inno Setup failed' }
        $setup = Join-Path $dist "AmqTool-Setup-v$version.exe"
    }
    else {
        Write-Warning 'Inno Setup 6 not found, skipped the installer (winget install JRSoftware.InnoSetup)'
    }
}

Write-Host ''
Write-Host "AmqTool v$version is packaged:" -ForegroundColor Green
foreach ($f in @($zip, $setup) | Where-Object { $_ }) { '  {0}  ({1:N1} MB)' -f $f, ((Get-Item $f).Length / 1MB) }
Write-Host ''
Write-Host "Release it: new GitHub release with tag v$version, attach the file(s) above. The zip is what in-app updates download."
