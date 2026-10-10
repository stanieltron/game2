param([string]$Cases,[float]$MaterialScale=1,[float]$PhaseScale=1,[float]$Step=.5,[int]$Workers=3)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load_optimized.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs","$PSScriptRoot/BalanceCases.cs")
foreach($phase in @([BootstrapFactory.Simulation]::BayPhases)+@([BootstrapFactory.Simulation]::FighterPhases)){$phase.seconds *= $PhaseScale;foreach($cost in $phase.cost){$cost.amount=[int][math]::Round($cost.amount*$MaterialScale)}}
[FactoryBalance.BalanceCases]::Run($Cases,$Step,$Workers)
