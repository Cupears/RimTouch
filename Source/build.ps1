param(
    [string]$RimWorldRoot = $env:RIMWORLD_ROOT,
    [string]$HarmonyDll = $env:HARMONY_DLL,
    [switch]$Test,
    [switch]$Package
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (!$RimWorldRoot -or !(Test-Path (Join-Path $RimWorldRoot 'UnityStubs.dll'))) {
    throw 'RimWorld 1.7 files missing. Set RIMWORLD_ROOT to the game directory.'
}
if (!$HarmonyDll) {
    $steamApps = Split-Path -Parent (Split-Path -Parent $RimWorldRoot)
    $candidate = Join-Path $steamApps 'workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll'
    if (Test-Path $candidate) { $HarmonyDll = $candidate }
}
if (!$HarmonyDll -or !(Test-Path $HarmonyDll)) { throw 'Set HARMONY_DLL to Harmony for RimWorld 1.7.' }
$buildArgs = @("-p:RimWorldRoot=$RimWorldRoot", "-p:HarmonyPath=$HarmonyDll")
& dotnet build (Join-Path $PSScriptRoot 'RimTouch.csproj') -c Release @buildArgs --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if ($Test) {
    $savedRoot = $env:RIMWORLD_ROOT
    try {
        $env:RIMWORLD_ROOT = $RimWorldRoot
        & dotnet run --project (Join-Path $root 'Tests/RimTouch.Tests.csproj') -c Release @buildArgs
        if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
    } finally { $env:RIMWORLD_ROOT = $savedRoot }
}
Copy-Item (Join-Path $root 'Build/RimTouch.dll') (Join-Path $root 'Assemblies/RimTouch.dll') -Force
if ($Package) {
    $publish = Join-Path $root 'Publish'
    $folder = Join-Path $publish ('RimTouch-1.7.0-beta.1-' + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff'))
    $mod = Join-Path $folder 'RimTouch-1.7-Beta'
    New-Item -ItemType Directory -Force -Path $mod | Out-Null
    foreach ($entry in @('About','Assemblies','assets','docs','README.md','CHANGELOG.md','LICENSE')) {
        Copy-Item (Join-Path $root $entry) $mod -Recurse
    }
    $zip = Join-Path $publish 'RimTouch-1.7.0-beta.1.zip'
    Compress-Archive -LiteralPath $mod -DestinationPath $zip -Force
    (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower() + '  RimTouch-1.7.0-beta.1.zip' |
        Set-Content (Join-Path $publish 'SHA256SUMS.txt')
    Write-Output "Package: $zip"
}
