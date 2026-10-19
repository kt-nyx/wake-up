param([Parameter(Mandatory=$true)][string]$GameDir,
      [Parameter(Mandatory=$true)][string]$HarmonyDir)
$ErrorActionPreference = 'Stop'
$contracts = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../src/WakeUp17/Contracts.json') -Raw | ConvertFrom-Json
foreach ($item in @(
    @{Path=(Join-Path $GameDir 'Assembly-CSharp.dll'); Hash=$contracts.gameSha256},
    @{Path=(Join-Path $GameDir 'UnityStubs.dll'); Hash=$contracts.unityStubsSha256},
    @{Path=(Join-Path $GameDir 'Waylith.dll'); Hash=$contracts.waylithSha256},
    @{Path=(Join-Path $HarmonyDir '0Harmony.dll'); Hash=$contracts.harmonySha256})) {
    if ((Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash -ne $item.Hash) {
        throw "Unreviewed 1.7 reference: $($item.Path). Refresh the static review before updating contracts."
    }
}
Write-Output 'Reviewed 1.7 game, engine, compatibility and Harmony references verified.'
