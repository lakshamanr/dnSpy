param(
    [string]$OutputDir = 'C:\dnSpy-release',
    [switch]$SkipClean  # skip inter-build cleans for faster local iteration
)
$ErrorActionPreference = 'Stop'
$startTime = Get-Date

# ── Validate prerequisites ───────────────────────────────────────────────────
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "git not found on PATH. Install Git for Windows and retry."
    exit 1
}

$buildScript = Join-Path $PSScriptRoot 'build.ps1'
if (-not (Test-Path $buildScript)) {
    Write-Error "build.ps1 not found at: $buildScript"
    exit 1
}

# ── Find MSBuild ─────────────────────────────────────────────────────────────
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found. Install Visual Studio with the MSBuild workload."
    exit 1
}
# Try amd64 first, fall back to any available MSBuild (covers ARM64 hosts)
$msbuildExe = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\Current\Bin\amd64\MSBuild.exe 2>$null | Select-Object -First 1
if (-not $msbuildExe) {
    $msbuildExe = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\MSBuild.exe 2>$null | Select-Object -First 1
}
if (-not $msbuildExe) {
    Write-Error "MSBuild not found. Install Visual Studio with the MSBuild workload."
    exit 1
}
$env:PATH = (Split-Path $msbuildExe -Parent) + ';' + $env:PATH
Write-Host "MSBuild : $msbuildExe"

# ── Read version from props ──────────────────────────────────────────────────
$propsFile = Join-Path $PSScriptRoot 'DnSpyCommon.props'
if (-not (Test-Path $propsFile)) {
    Write-Error "DnSpyCommon.props not found at: $propsFile"
    exit 1
}
$propsXml = [xml](Get-Content $propsFile -ErrorAction Stop)
# PropertyGroup may be an array if there are multiple groups - pick the one with the version
$version = $propsXml.Project.PropertyGroup |
    ForEach-Object { $_.DnSpyAssemblyInformationalVersion } |
    Where-Object { $_ } |
    Select-Object -First 1
if (-not $version) {
    Write-Error "DnSpyAssemblyInformationalVersion not found in DnSpyCommon.props"
    exit 1
}
Write-Host "Version : $version"

# ── Restore submodules ───────────────────────────────────────────────────────
Write-Host "Restoring submodules ..."
git -C $PSScriptRoot submodule update --init --recursive
if ($LASTEXITCODE) { Write-Error "git submodule restore failed (exit $LASTEXITCODE)"; exit 1 }

# ── Output directory ─────────────────────────────────────────────────────────
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
if (-not (Test-Path $OutputDir)) {
    Write-Error "Failed to create output directory: $OutputDir"
    exit 1
}
Write-Host "Output  : $OutputDir`n"

# ── Helpers ──────────────────────────────────────────────────────────────────
function Invoke-Build([string]$target) {
    Write-Host "=== Building $target ===" -ForegroundColor Cyan
    # Run build.ps1 in a child PowerShell process so its `exit` calls don't
    # terminate this script, and $LASTEXITCODE is always populated correctly.
    $psi = [System.Diagnostics.ProcessStartInfo]@{
        FileName               = (Get-Command powershell.exe).Source
        Arguments              = "-NoProfile -ExecutionPolicy Bypass -File `"$buildScript`" $target"
        WorkingDirectory       = $PSScriptRoot
        UseShellExecute        = $false
        RedirectStandardOutput = $false
        RedirectStandardError  = $false
    }
    $proc = [System.Diagnostics.Process]::Start($psi)
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) {
        Write-Error "$target build failed (exit $($proc.ExitCode))"
        exit $proc.ExitCode
    }
}

function New-ReleaseZip([string]$sourceDir, [string]$zipName) {
    $dest = Join-Path $OutputDir $zipName
    # Validate the source directory exists and has content before zipping
    $resolvedDir = $sourceDir.TrimEnd('\', '/', '*')
    if (-not (Test-Path $resolvedDir)) {
        Write-Error "Build output directory not found: $resolvedDir`nThe build step may have failed silently."
        exit 1
    }
    $fileCount = (Get-ChildItem -Path $resolvedDir -Recurse -File).Count
    if ($fileCount -eq 0) {
        Write-Error "Build output directory is empty: $resolvedDir"
        exit 1
    }
    Write-Host "Zipping $zipName  ($fileCount files) ..."
    Compress-Archive -Path "$resolvedDir\*" -DestinationPath $dest -Force
    $mb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
    Write-Host "  -> $zipName  ($mb MB)"
}

function Invoke-Clean {
    if ($SkipClean) { Write-Host "(skipping clean)"; return }
    Write-Host "Cleaning ..."
    $cleanCmd = Join-Path $PSScriptRoot 'clean-all.cmd'
    if (-not (Test-Path $cleanCmd)) {
        Write-Warning "clean-all.cmd not found - skipping clean step"
    }
    else {
        # clean-all.cmd may exit 1 when VS locks index files - treat non-zero as a warning, not a failure
        cmd /c "`"$cleanCmd`""
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            Write-Warning "clean-all.cmd exited $LASTEXITCODE (probably VS file locks) - continuing"
        }
        $global:LASTEXITCODE = 0
    }
    Write-Host ""
}

# ── .NET Framework ────────────────────────────────────────────────────────────
Invoke-Build netframework
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net48" 'dnSpy-netframework.zip'
Invoke-Clean

# ── .NET win-x86 ─────────────────────────────────────────────────────────────
Invoke-Build net-x86
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net5.0-windows\win-x86\publish" 'dnSpy-net-win32.zip'
Invoke-Clean

# ── .NET win-x64 ─────────────────────────────────────────────────────────────
Invoke-Build net-x64
New-ReleaseZip "$PSScriptRoot\dnSpy\dnSpy\bin\Release\net5.0-windows\win-x64\publish" 'dnSpy-net-win64.zip'
Invoke-Clean

# ── MSI installers (x64 + x86) ────────────────────────────────────────────────
$msiScript = Join-Path $PSScriptRoot 'Build\Installer\build-msi.ps1'
if (Test-Path $msiScript) {
    Write-Host "=== Building MSI installers ===" -ForegroundColor Cyan
    & $msiScript -OutputDir $OutputDir
    if ($LASTEXITCODE) { Write-Error "MSI build failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
}
else {
    Write-Host "Skipping MSI: $msiScript not found" -ForegroundColor Yellow
}

# ── Summary ───────────────────────────────────────────────────────────────────
$elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
Write-Host ""
Write-Host "=== Release $version ready  ($elapsed min) ===" -ForegroundColor Green
Get-Item "$OutputDir\*.zip" | ForEach-Object {
    $mb = [math]::Round($_.Length / 1MB, 1)
    Write-Host "  $($_.Name)  $mb MB"
}
Write-Host "`nArtifacts saved to: $OutputDir" -ForegroundColor Green
exit 0

