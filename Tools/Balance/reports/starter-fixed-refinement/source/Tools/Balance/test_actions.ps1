param([string]$Catalog="$PSScriptRoot/../../Assets/Resources/catalog.json")
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/ActionSearch.cs","$PSScriptRoot/ActionTests.cs")
[FactoryBalance.ActionTests]::Run($Catalog,"$PSScriptRoot/spec")
