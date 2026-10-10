$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/IndustrialStep.cs")
[FactoryBalance.IndustrialStepStudy]::Run("$PSScriptRoot/../../Assets/Resources/catalog.json","$PSScriptRoot/reports/industrial-step-v1")
