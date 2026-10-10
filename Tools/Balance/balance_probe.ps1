param([string]$Catalog,[string]$Policy,[string]$Output,[float]$PhaseScale=1,[float]$Step=.5)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
# Experimental timings are isolated to this process; never write game sources here.
foreach($phase in @([BootstrapFactory.Simulation]::BayPhases)+@([BootstrapFactory.Simulation]::FighterPhases)){$phase.seconds *= $PhaseScale}
[FactoryBalance.BatchSearch]::Replay($Catalog,$Policy,$Output,$Step)
