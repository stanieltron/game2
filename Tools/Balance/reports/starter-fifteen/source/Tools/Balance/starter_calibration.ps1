param([string]$Cases,[string]$Output,[int]$Workers=15)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/StarterPaths.cs","$PSScriptRoot/StarterCalibration.cs")
[FactoryBalance.StarterCalibration]::Run($Cases,$Output,$Workers)
