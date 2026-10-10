$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/StarterPaths.cs")
[FactoryBalance.StarterPaths]::Verify("$PSScriptRoot/reports/starter-paths-v3")
