param([string]$buildtfm = 'all', [switch]$NoMsbuild)
$ErrorActionPreference = 'Stop'

# Anchor all relative paths to the repo root regardless of the caller's CWD
Push-Location $PSScriptRoot
try {

$netframework_tfm = 'net48'
$net_tfm = 'net5.0-windows'
$configuration = 'Release'
$net_baseoutput = "dnSpy\dnSpy\bin\$configuration"
$apphostpatcher_dir = "Build\AppHostPatcher"

#
# dotnet build does not support COM references — we use MSBuild directly.
# https://github.com/dnSpy/dnSpy/issues/1053
#

function Build-NetFramework {
	Write-Host 'Building .NET Framework x86 and x64 binaries'

	$outdir = "$net_baseoutput\$netframework_tfm"

	if ($NoMsbuild) {
		dotnet build -v:m -c $configuration -f $netframework_tfm
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}
	else {
		msbuild -v:m -m -restore -t:Build -p:Configuration=$configuration -p:TargetFramework=$netframework_tfm
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}

	if (-not (Test-Path $outdir)) {
		Write-Error "Expected output directory not found after build: $outdir"
		exit 1
	}

	# Reorganise: move all files into a bin\ subdirectory, keep exe/config/pdb at root
	$binTemp = "$net_baseoutput\bin"
	if (Test-Path $binTemp) { Remove-Item $binTemp -Recurse -Force }
	Rename-Item $outdir bin
	New-Item -ItemType Directory $outdir | Out-Null
	Move-Item $binTemp $outdir  # now at $outdir\bin

	foreach ($filename in 'dnSpy-x86.exe', 'dnSpy-x86.exe.config', 'dnSpy-x86.pdb',
			 'dnSpy.exe', 'dnSpy.exe.config', 'dnSpy.pdb',
			 'dnSpy.Console.exe', 'dnSpy.Console.exe.config', 'dnSpy.Console.pdb') {
		$src = "$outdir\bin\$filename"
		if (Test-Path $src) {
			Move-Item $src $outdir
		}
		else {
			Write-Warning "Expected file not found (skipping): $src"
		}
	}
}

function Build-Net {
	param([string]$arch)

	Write-Host "Building .NET $arch binaries"

	$rid = "win-$arch"
	$outdir = "$net_baseoutput\$net_tfm\$rid"
	$publishDir = "$outdir\publish"

	if ($NoMsbuild) {
		dotnet publish -v:m -c $configuration -f $net_tfm -r $rid --self-contained
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}
	else {
		msbuild -v:m -m -restore -t:Publish -p:Configuration=$configuration -p:TargetFramework=$net_tfm -p:RuntimeIdentifier=$rid -p:SelfContained=True
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}

	if (-not (Test-Path $publishDir)) {
		Write-Error "Expected publish directory not found after build: $publishDir"
		exit 1
	}

	# Reorganise: move all files into bin\, keep exe apphosts at root
	$tmpbin = "$outdir\tmpbin"
	if (Test-Path $tmpbin) { Remove-Item $tmpbin -Recurse -Force }
	Rename-Item $publishDir tmpbin            # publishDir → outdir\tmpbin
	New-Item -ItemType Directory $publishDir | Out-Null
	Move-Item $tmpbin "$publishDir\bin"       # outdir\tmpbin → publishDir\bin

	$patcherExe = "$apphostpatcher_dir\bin\$configuration\$netframework_tfm\AppHostPatcher.exe"
	if (-not (Test-Path $patcherExe)) {
		Write-Error "AppHostPatcher.exe not found: $patcherExe`nBuild AppHostPatcher first."
		exit 1
	}

	foreach ($exe in 'dnSpy.exe', 'dnSpy.Console.exe') {
		$src = "$publishDir\bin\$exe"
		if (-not (Test-Path $src)) {
			Write-Warning "Expected exe not found (skipping AppHostPatcher for): $src"
			continue
		}
		Move-Item $src $publishDir
		& $patcherExe "$publishDir\$exe" -d bin
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}
}

$validTargets = @('all', 'netframework', 'net-x86', 'net-x64')
if ($buildtfm -notin $validTargets) {
	Write-Error "Unknown build target '$buildtfm'. Valid values: $($validTargets -join ', ')"
	exit 1
}

$buildNet    = $buildtfm -eq 'all' -or $buildtfm -eq 'netframework'
$buildNetX86 = $buildtfm -eq 'all' -or $buildtfm -eq 'net-x86'
$buildNetX64 = $buildtfm -eq 'all' -or $buildtfm -eq 'net-x64'

if ($buildNetX86 -or $buildNetX64) {
	if ($NoMsbuild) {
		dotnet build -v:m -c $configuration -f $netframework_tfm $apphostpatcher_dir\AppHostPatcher.csproj
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}
	else {
		msbuild -v:m -m -restore -t:Build -p:Configuration=$configuration -p:TargetFramework=$netframework_tfm $apphostpatcher_dir\AppHostPatcher.csproj
		if ($LASTEXITCODE) { exit $LASTEXITCODE }
	}
}

if ($buildNet)    { Build-NetFramework }
if ($buildNetX86) { Build-Net x86 }
if ($buildNetX64) { Build-Net x64 }

} finally {
	Pop-Location
}

