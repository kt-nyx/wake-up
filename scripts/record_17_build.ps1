# Static inspection of the compiled product; does not load its code or the game.
param([Parameter(Mandatory=$true)][string]$Dll,
      [Parameter(Mandatory=$true)][string]$CecilPath,
      [Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -LiteralPath $CecilPath
$metadata = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll)
if ($metadata.Name.Name -ne 'WakeUp17') { throw 'Not a WakeUp17 product assembly.' }
$references = @($metadata.MainModule.AssemblyReferences | ForEach-Object Name)
if ('Assembly-CSharp' -notin $references -or '0Harmony' -notin $references) { throw 'Missing product references.' }
if ($references | Where-Object { $_ -match '(?i)prepatcher|cecil|NAudio|^UnityEngine\.' }) {
    throw 'Unexpected legacy/prepatcher dependency in the 1.7 runtime.'
}
$information = $metadata.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyInformationalVersionAttribute' }
$version = $information.ConstructorArguments[0].Value
if ($version -notmatch '\+([a-f0-9]{40})$') { throw 'Build has no immutable source revision.' }
$result = [ordered]@{
    dll=(Resolve-Path -LiteralPath $Dll).Path
    dllSha256=(Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant()
    mvid=$metadata.MainModule.Mvid.ToString()
    informationalVersion=$version
    buildSourceRevision=$Matches[1]
    references=$references
    prepatcherFree=$true
    liveTestsPerformed=$false
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$metadata.Dispose()
Write-Output ($result | ConvertTo-Json -Depth 4)
