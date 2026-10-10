param([string]$Command='search',[string]$Catalog="$PSScriptRoot/../../Assets/Resources/catalog.json",[string]$Output="$PSScriptRoot/reports/actions-v1",[string]$Program,[string]$Progress="$PSScriptRoot/web/action-progress.json",[int]$Trials=1024,[int]$Workers=15,[int]$Seed=731,[int]$BatchSize=64,[float]$Step=.5)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/ActionPlanner.cs","$PSScriptRoot/ActionSearch.cs")
if($Command -eq 'replay'){
    [FactoryBalance.ActionSearch]::ReplayFile($Catalog,$Program,$Output,$Step)
} elseif($Command -eq 'search'){
    [FactoryBalance.ActionSearch]::Run($Catalog,"$PSScriptRoot/spec",$Output,$Progress,$Trials,$Seed,$Workers,$Step,$BatchSize)
} else {throw 'Use search or replay'}
