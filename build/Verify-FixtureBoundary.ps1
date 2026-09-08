# Copyright (c) 2026 kt-nyx and contributors.
# Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$paths = @(& git -C $RepositoryRoot ls-files --cached -- '.rlo-test-instance' ':(glob)**/.rlo-test-instance/**')
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Git index.' }
if ($paths.Count -ne 0) { throw 'Private fixture payload is in the Git index. Unstage it before committing.' }
$probe = '.rlo-test-instance/game/fixture-ignore-probe'
& git -C $RepositoryRoot check-ignore -q -- $probe
if ($LASTEXITCODE -ne 0) { throw 'Fixture ignore rule is missing.' }
Write-Output 'FIXTURE_GIT_BOUNDARY_OK'
