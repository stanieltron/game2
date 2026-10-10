$ErrorActionPreference='Stop'
throw 'Historical experiment: use archived pre-v4 phase source in an isolated copy.'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
$p="$PSScriptRoot/reports/six-v4"
foreach($phase in @([BootstrapFactory.Simulation]::BayPhases)+@([BootstrapFactory.Simulation]::FighterPhases)){foreach($cost in $phase.cost){$cost.amount *= 3}}
foreach($name in @('minimum','n5','n7','n10')) {
 $policy=if($name -eq 'minimum'){"$PSScriptRoot/reports/binary-v3/minimum.policy.json"}else{"$p/$name.policy.json"}
 [FactoryBalance.BatchSearch]::Replay("$p/fast4.catalog.json",$policy,"$p/heavy4-$name.json",.5)
 $r=Get-Content "$p/heavy4-$name.json" -Raw|ConvertFrom-Json
 Write-Output "heavy4 $name minutes=$([math]::Round($r.seconds/60,2))"
}
