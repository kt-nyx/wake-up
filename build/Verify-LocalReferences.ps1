# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

[CmdletBinding()]
param([switch]$DefinitionsOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not ('ReferenceMetadataInspector' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

public static class ReferenceMetadataInspector
{
    public static string GetTargetFramework(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var pe = new PEReader(stream))
        {
            MetadataReader reader = pe.GetMetadataReader();
            foreach (CustomAttributeHandle handle in reader.CustomAttributes)
            {
                CustomAttribute attribute = reader.GetCustomAttribute(handle);
                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                    continue;
                MemberReference constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference)
                    continue;
                TypeReference type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                if (reader.GetString(type.Namespace) != "System.Runtime.Versioning"
                    || reader.GetString(type.Name) != "TargetFrameworkAttribute")
                    continue;
                BlobReader blob = reader.GetBlobReader(attribute.Value);
                if (blob.ReadUInt16() != 1)
                    throw new InvalidDataException("Invalid TargetFrameworkAttribute prolog.");
                return blob.ReadSerializedString();
            }
        }
        return null;
    }
}
'@
}

function Fail([string]$Message) {
    throw "Local-reference verification failed: $Message"
}

function Verify-Reference {
    param(
        [string]$Path,
        [string]$ExpectedName,
        [string]$ExpectedVersion,
        [string]$ExpectedSha256,
        [string]$ExpectedTargetFramework
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail "missing '$Path'"
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $actualHash = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
    if ($actualHash -ne $ExpectedSha256) {
        Fail "SHA-256 mismatch for '$resolved': expected $ExpectedSha256, got $actualHash"
    }

    $identity = [System.Reflection.AssemblyName]::GetAssemblyName($resolved)
    if ($identity.Name -cne $ExpectedName) {
        Fail "assembly-name mismatch for '$resolved': expected $ExpectedName, got $($identity.Name)"
    }

    if ($identity.Version.ToString() -cne $ExpectedVersion) {
        Fail "assembly-version mismatch for '$resolved': expected $ExpectedVersion, got $($identity.Version)"
    }

    $actualFramework = [ReferenceMetadataInspector]::GetTargetFramework($resolved)
    $actualDisplay = if ($null -eq $actualFramework) { '<absent>' } else { $actualFramework }
    if ($actualDisplay -cne $ExpectedTargetFramework) {
        Fail "target-framework mismatch for '$resolved': expected $ExpectedTargetFramework, got $actualDisplay"
    }

    [pscustomobject]@{
        Path = $resolved
        Assembly = $identity.Name
        Version = $identity.Version.ToString()
        TargetFramework = $actualDisplay
        Sha256 = $actualHash
    }
}

if ($DefinitionsOnly) {
    return
}

$managedDir = $env:WAKE_UP_RIMWORLD_MANAGED_DIR
$prepatcherDir = $env:WAKE_UP_PREPATCHER_ASSEMBLIES_DIR
if ([string]::IsNullOrWhiteSpace($managedDir)) {
    Fail 'WAKE_UP_RIMWORLD_MANAGED_DIR is not set'
}
if ([string]::IsNullOrWhiteSpace($prepatcherDir)) {
    Fail 'WAKE_UP_PREPATCHER_ASSEMBLIES_DIR is not set'
}

$managedDir = (Resolve-Path -LiteralPath $managedDir).Path
$prepatcherDir = (Resolve-Path -LiteralPath $prepatcherDir).Path

# The fixture workflow explicitly selects the owner-approved GOG development
# reference. GameBuildContract separately authenticates reviewed runtimes.
# An unspecified target retains the historical reviewed Steam reference.
$referenceTarget = $env:WAKE_UP_REFERENCE_TARGET
$gameVersion = '1.6.9676.17735'
$gameSha = '5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A'
if ($referenceTarget -eq 'gog-rev573') {
    $gameVersion = '1.6.9676.17238'
    $gameSha = '4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28'
}
elseif (-not [string]::IsNullOrWhiteSpace($referenceTarget) -and $referenceTarget -ne 'steam-rev590') {
    if ($referenceTarget -eq 'linux-rev600') {
        $gameVersion = '1.6.9676.18020'
        $gameSha = '082DB1DD4F7F1D0B72960D7E1BEEAD8FBFE6957200E8627F65BDA0DBBE1DD8F8'
    }
    else { Fail ('unknown WAKE_UP_REFERENCE_TARGET: ' + $referenceTarget) }
}

$verified = @(
    Verify-Reference -Path (Join-Path $prepatcherDir '0PrepatcherAPI.dll') -ExpectedName '0PrepatcherAPI' -ExpectedVersion '1.2.0.0' -ExpectedSha256 '39A3841D1C61C41D173E5CFB81262FDE78DB655A0D604A2DD91A5697E9D57300' -ExpectedTargetFramework '.NETFramework,Version=v4.7.2'
    Verify-Reference -Path (Join-Path $managedDir 'Assembly-CSharp.dll') -ExpectedName 'Assembly-CSharp' -ExpectedVersion $gameVersion -ExpectedSha256 $gameSha -ExpectedTargetFramework '<absent>'
    Verify-Reference -Path (Join-Path $prepatcherDir '0Harmony.dll') -ExpectedName '0Harmony' -ExpectedVersion '2.4.2.0' -ExpectedSha256 '7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C' -ExpectedTargetFramework '.NETFramework,Version=v4.7.2'
)

$verified | Format-Table -AutoSize | Out-Host
Write-Output 'LocalReferenceVerification=True'
Write-Output 'DirectReferenceCount=3'
