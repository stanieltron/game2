param([string]$Cases,[string]$Output,[int]$Workers=15)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/StarterPaths.cs","$PSScriptRoot/FighterProgression.cs")
[FactoryBalance.FighterProgression]::Batch($Cases,$Output,$Workers)
