param([bool]$LandingStock=$false,[bool]$ReplaceAll=$true,[string]$Output="$PSScriptRoot/reports/starter-paths-v1")
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/StarterPaths.cs")
[FactoryBalance.StarterPaths]::Run("$PSScriptRoot/../../Assets/Resources/catalog.json",$Output,$LandingStock,$ReplaceAll)
