param([string]$Catalog = "$PSScriptRoot/../../Assets/Resources/catalog.json", [string]$Output = "$PSScriptRoot/reports/planner", [float]$Step = 0.5, [switch]$LogisticsOnly)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs", "$PSScriptRoot/FactoryPlanner.cs")
if ($Step -le 0 -or $Step -gt 0.5) { throw 'Step must be in (0, 0.5].' }
if ($LogisticsOnly) {
    [IO.Directory]::CreateDirectory($Output) | Out-Null
    [FactoryBalance.FactoryPlanner]::LogisticsCheck($Catalog,$Output)
} else { [FactoryBalance.FactoryPlanner]::Compare($Catalog,$Output,$Step) }
