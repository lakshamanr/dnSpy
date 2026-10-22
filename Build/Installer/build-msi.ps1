param(
    [string]$OutputDir = 'C:\dnSpy-msi',
    [string]$Configuration = 'Release',
    [string]$WixDir = 'C:\Tools\WiX3'
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot | Split-Path -Parent | Split-Path -Parent
$candle = Join-Path $WixDir 'candle.exe'
$light = Join-Path $WixDir 'light.exe'
$heat = Join-Path $WixDir 'heat.exe'
foreach ($tool in $candle, $light, $heat) {
    if (-not (Test-Path $tool)) {
        Write-Error "$tool not found. Install the WiX Toolset v3 (LGPL) into '$WixDir' or pass -WixDir."
        exit 1
    }
}

$propsFile = Join-Path $repoRoot 'DnSpyCommon.props'
$version = ([xml](Get-Content $propsFile)).Project.PropertyGroup.DnSpyAssemblyInformationalVersion
# MSI Product/@Version must be x.x.x with integer parts, no 'v' prefix and no whitespace.
$msiVersion = (([string]$version).Trim() -replace '^v', '')
if ($msiVersion -notmatch '^\d+(\.\d+)*$') {
    Write-Error "Cannot derive a valid MSI version from '$version'."
    exit 1
}
Write-Host "Version : $version (MSI: $msiVersion)"

# x64 and x86 are separate products with separate UpgradeCodes, so installing one
# never removes or upgrades the other. The x64 package installs into Program Files
# (x64) and the x86 package into Program Files (x86).
$architectures = @(
    @{ Rid = 'win-x64'; Name = 'x64'; Arch = 'x64'; ProgramFilesId = 'ProgramFiles64Folder' },
    @{ Rid = 'win-x86'; Name = 'x86'; Arch = 'x86'; ProgramFilesId = 'ProgramFilesFolder' }
)

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

foreach ($arch in $architectures) {
    $rid = $arch.Rid
    $sourceDir = Join-Path $repoRoot "dnSpy\dnSpy\bin\$Configuration\net5.0-windows\$rid\publish"
    if (-not (Test-Path $sourceDir)) {
        Write-Error "Publish output not found for $rid : $sourceDir"
        exit 1
    }
    $binDir = Join-Path $sourceDir 'bin'
    if (-not (Test-Path $binDir)) {
        Write-Error "Expected '$binDir' to exist. Run build.ps1 so the publish dir is restructured (bin\ subdir + patched apphosts)."
        exit 1
    }
    foreach ($required in 'dnSpy.exe', 'dnSpy.Console.exe') {
        if (-not (Test-Path (Join-Path $sourceDir $required))) {
            Write-Error "Missing $required in $sourceDir"
            exit 1
        }
    }

    $isX64 = $arch.Name -eq 'x64'
    # x64 and x86 are separate products: different UpgradeCode means installing
    # one never removes or upgrades the other.
    $upgradeGuid = if ($isX64) { '9E1B7A44-2C6D-4E1B-9F3A-5D8C7B2E4A11' } else { '3F7C2E19-8B4D-4A62-9C1F-7E5A3D9B6C28' }
    $iconFile = if ($isX64) {
        Join-Path $repoRoot 'dnSpy\dnSpy\Images\dnSpy.ico'
    } else {
        Join-Path $repoRoot 'dnSpy\dnSpy\Images\dnSpy-x86.ico'
    }

    $objDir = Join-Path $repoRoot "Build\Installer\obj\$rid"
    if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }
    New-Item -ItemType Directory -Path $objDir -Force | Out-Null
    $wxs = Join-Path $repoRoot 'Build\Installer\dnSpy.wxs'
    # candle needs a directory (not a file) for -out when compiling multiple sources
    $wixObjDir = Join-Path $objDir 'obj'
    $wixobj = Join-Path $wixObjDir 'dnSpy.wixobj'
    $msi = Join-Path $OutputDir "dnSpy-$($arch.Name)-$msiVersion.msi"

    Write-Host "=== Building MSI for $rid ===" -ForegroundColor Cyan

    # Harvest the whole publish dir (dnSpy.exe, dnSpy.Console.exe, and the bin\
    # payload with the runtime, extensions, debug\core, Themes, ...) into a
    # component group. -gg generates stable GUIDs now so re-runs are idempotent.
    # -scom/-sreg skip COM/registry harvesting: these are .NET assemblies, and heat
    # otherwise tries to SelfReg every DLL (HEAT5150) and fails.
    $harvestWxs = Join-Path $objDir 'Payload.wxs'
    & $heat dir "$sourceDir" -nologo -ag -cg Payload -directoryid INSTALLFOLDER `
        -dr INSTALLFOLDER -var var.SourceDir -gg -srd -sfrag -scom -sreg -sw5150 `
        -out $harvestWxs
    if ($LASTEXITCODE) { Write-Error "heat failed for $rid (exit $LASTEXITCODE)"; exit $LASTEXITCODE }

    New-Item -ItemType Directory -Path $wixObjDir -Force | Out-Null
    & $candle -nologo -arch $arch.Arch `
        -dSourceDir="$sourceDir" `
        -dProductVersion="$msiVersion" `
        -dUpgradeCode="$upgradeGuid" `
        -dProgramFilesId="$($arch.ProgramFilesId)" `
        -dIconFile="$iconFile" `
        -out "$wixObjDir\" $wxs $harvestWxs
    if ($LASTEXITCODE) { Write-Error "candle failed for $rid (exit $LASTEXITCODE)"; exit $LASTEXITCODE }

    & $light -nologo -sice:ICE61 -sice:ICE91 `
        -out $msi `
        -ext WixUIExtension `
        (Get-ChildItem "$wixObjDir\*.wixobj" | Select-Object -ExpandProperty FullName)
    if ($LASTEXITCODE) { Write-Error "light failed for $rid (exit $LASTEXITCODE)"; exit $LASTEXITCODE }

    $size = [math]::Round((Get-Item $msi).Length / 1MB, 1)
    Write-Host "  -> $msi ($size MB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Artifacts saved to: $OutputDir" -ForegroundColor Green
Get-ChildItem "$OutputDir\*.msi" | ForEach-Object {
    Write-Host ("  {0,-52} {1,6:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
