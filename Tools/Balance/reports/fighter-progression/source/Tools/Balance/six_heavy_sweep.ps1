$ErrorActionPreference='Stop'
throw 'Historical experiment: restore reports/six-v4/before.FactoryMegaproject.cs.txt in an isolated copy before using these scale factors.'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
$p="$PSScriptRoot/reports/six-v4"
foreach($phase in @([BootstrapFactory.Simulation]::BayPhases)+@([BootstrapFactory.Simulation]::FighterPhases)){foreach($cost in $phase.cost){$cost.amount *= 3}}
foreach($factor in @(4,6)) {foreach($n in @(4,6,8,12)) {
 $output="$p/heavy$factor-n$n.json"
 [FactoryBalance.BatchSearch]::Replay("$p/fast$factor.catalog.json","$p/n$n.policy.json",$output,.5)
 $r=Get-Content $output -Raw|ConvertFrom-Json
 Write-Output "heavy cost=$factor n=$n success=$($r.success) minutes=$([math]::Round($r.seconds/60,2))"
}}
