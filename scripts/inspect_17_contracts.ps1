# Static PE/metadata inspection only. Loading Cecil does not load game assemblies.
param([Parameter(Mandatory=$true)][string]$GameDir,
      [Parameter(Mandatory=$true)][string]$HarmonyDir,
      [Parameter(Mandatory=$true)][string]$CecilPath,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath $CecilPath
$reviewTypes = @('Verse.LoadedModManager','Verse.Root_Entry','Verse.GenTypes',
    'RimWorld.AlertsReadout','Verse.DirectXmlCrossRefLoader','Verse.DefInjectionPackage',
    'Verse.GenGeneric','Verse.XmlToObjectUtils','Verse.XmlHelper')
$gameFile = Join-Path $GameDir 'Assembly-CSharp.dll'
$gameMetadata = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($gameFile)
$harmonyMetadata = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $HarmonyDir '0Harmony.dll'))
$bytes = [IO.File]::ReadAllBytes($gameFile)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3c)
$sectionCount = [BitConverter]::ToUInt16($bytes, $peOffset + 6)
$optionalSize = [BitConverter]::ToUInt16($bytes, $peOffset + 20)
$sectionOffset = $peOffset + 24 + $optionalSize
function Get-CodeBytes($method) {
    $rva = $method.RVA
    for ($section=0; $section -lt $sectionCount; $section++) {
        $at = $sectionOffset + 40 * $section
        $size = [Math]::Max([BitConverter]::ToUInt32($bytes,$at+8),[BitConverter]::ToUInt32($bytes,$at+16))
        $start = [BitConverter]::ToUInt32($bytes,$at+12)
        if ($rva -ge $start -and $rva -lt $start+$size) {
            $offset = [int]($rva-$start+[BitConverter]::ToUInt32($bytes,$at+20))
            if (($bytes[$offset] -band 3) -eq 2) { $length=$bytes[$offset] -shr 2; $header=1 }
            else { $length=[BitConverter]::ToInt32($bytes,$offset+4); $header=([BitConverter]::ToUInt16($bytes,$offset) -shr 12)*4 }
            return ,$bytes[($offset+$header)..($offset+$header+$length-1)]
        }
    }
    throw "Method RVA outside file: $method"
}
$methods = [ordered]@{}
foreach ($type in $gameMetadata.MainModule.GetTypes()) {
    $selected = $reviewTypes -contains $type.FullName -or $type.FullName -like 'Verse.PatchOperation*'
    foreach ($parent in $reviewTypes) { if ($type.FullName.StartsWith($parent + '/')) { $selected = $true } }
    if (!$selected) { continue }
    foreach ($method in $type.Methods) {
        if (!$method.HasBody) { continue }
        $codeBytes = Get-CodeBytes $method
        $sha = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$codeBytes)).ToLowerInvariant()
        $methods[$method.MetadataToken.ToInt32().ToString()] = [ordered]@{signature=$method.FullName; ilSha256=$sha}
    }
}
$result = [ordered]@{
    gameLabel=(Get-Content -LiteralPath (Join-Path $GameDir 'Version.txt') -Raw).Trim()
    gameMvid=$gameMetadata.MainModule.Mvid.ToString()
    gameSha256=(Get-FileHash -LiteralPath $gameFile -Algorithm SHA256).Hash.ToLowerInvariant()
    unityStubsSha256=(Get-FileHash -LiteralPath (Join-Path $GameDir 'UnityStubs.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    waylithSha256=(Get-FileHash -LiteralPath (Join-Path $GameDir 'Waylith.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    harmonyMvid=$harmonyMetadata.MainModule.Mvid.ToString()
    harmonySha256=(Get-FileHash -LiteralPath (Join-Path $HarmonyDir '0Harmony.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
    methods=$methods
}
$json = ($result | ConvertTo-Json -Depth 6).Replace("`r`n", "`n") + "`n"
[IO.File]::WriteAllText($OutputPath, $json, [Text.UTF8Encoding]::new($false))
$gameMetadata.Dispose(); $harmonyMetadata.Dispose()
Write-Output "Statically inspected $($methods.Count) method bodies. Contract output: $OutputPath"
