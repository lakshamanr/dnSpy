param(
    [string]$OutputDir = 'C:\dnSpy-release'
)
$ErrorActionPreference = 'Stop'

# ── Find MSBuild ────────────────────────────────────────────────────────────
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found. Install Visual Studio with the MSBuild workload."
    exit 1
}
$msbuildExe = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\Current\Bin\amd64\MSBuild.exe | Select-Object -First 1
if (-not $msbuildExe) {
    Write-Error "MSBuild not found. Install Visual Studio with the MSBuild workload."
    exit 1
}
$env:PATH = (Split-Path $msbuildExe -Parent) + ';' + $env:PATH
Write-Host "MSBuild : $msbuildExe"

# ── Read version from props ─────────────────────────────────────────────────
$propsFile = Join-Path $PSScriptRoot 'DnSpyCommon.props'
$version = ([xml](Get-Content $propsFile)).Project.PropertyGroup.DnSpyAssemblyInformationalVersion
Write-Host "Version : $version"

# ── Restore submodules (git clean wipes them during build cycles) ────────────
Write-Host "Restoring submodules ..."
git -C $PSScriptRoot submodule update --init --recursive
if ($LASTEXITCODE) { Write-Error "submodule restore failed"; exit $LASTEXITCODE }

# ── Output directory ─────────────────────────────────────────────────────────
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Write-Host "Output  : $OutputDir`n"

# ── Helpers ──────────────────────────────────────────────────────────────────
function Invoke-Build([string]$target) {
    Write-Host "=== Building $target ===" -ForegroundColor Cyan
    & "$PSScriptRoot\build.ps1" $target
    if ($LASTEXITCODE) { Write-Error "$target build failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
}

function New-ReleaseZip([string]$sourceGlob, [string]$zipName) {
    $dest = Join-Path $OutputDir $zipName
    Write-Host "Zipping $zipName ..."
    Compress-Archive -Path $sourceGlob -DestinationPath $dest -Force
    $mb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
    Write-Host "  -> $zipName  ($mb MB)"
}

function Invoke-Clean {
    Write-Host "Cleaning ..."
    # clean-all.cmd exits 1 when VS index files are locked by a running IDE; harmless
    cmd /c "$PSScriptRoot\clean-all.cmd"
    $global:LASTEXITCODE = 0   # prevent the 1 from bleeding into the script exit code
    Write-Host ""
}

# ── .NET Framework ────────────────────────────────────────────────────────────
Invoke-Build netframework
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net48\*" 'dnSpy-netframework.zip'
Invoke-Clean

# ── .NET win-x86 ─────────────────────────────────────────────────────────────
Invoke-Build net-x86
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net5.0-windows\win-x86\publish\*" 'dnSpy-net-win32.zip'
Invoke-Clean

# ── .NET win-x64 ─────────────────────────────────────────────────────────────
Invoke-Build net-x64
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net5.0-windows\win-x64\publish\*" 'dnSpy-net-win64.zip'
Invoke-Clean

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host "=== Release $version ready ===" -ForegroundColor Green
Get-Item "$OutputDir\*.zip" | ForEach-Object {
    $mb = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name)  $mb MB"
}
Write-Host "`nArtifacts saved to: $OutputDir" -ForegroundColor Green
exit 0
