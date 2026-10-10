$ErrorActionPreference='Stop'
throw 'Historical experiment: restore reports/six-v4/before.FactoryMegaproject.cs.txt in an isolated copy before using these scale factors.'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
$p="$PSScriptRoot/reports/six-v4"
foreach($factor in @(2,4)) {foreach($n in @(4,6,8,12)) {
 $output="$p/fast$factor-n$n.json"
 [FactoryBalance.BatchSearch]::Replay("$p/fast$factor.catalog.json","$p/n$n.policy.json",$output,.5)
 $r=Get-Content $output -Raw|ConvertFrom-Json
 Write-Output "cost=$factor n=$n success=$($r.success) minutes=$([math]::Round($r.seconds/60,2))"
}}

