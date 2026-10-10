$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/MixedProductionTest.cs")
[FactoryBalance.FactoryPlanner]::VerifyMixed("$PSScriptRoot/../../Assets/Resources/catalog.json")
