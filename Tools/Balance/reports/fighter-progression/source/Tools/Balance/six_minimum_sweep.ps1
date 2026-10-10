$ErrorActionPreference='Stop'
throw 'Historical experiment: restore reports/six-v4/before.FactoryMegaproject.cs.txt in an isolated copy before using these scale factors.'
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
$p="$PSScriptRoot/reports/six-v4"
foreach($profile in @('fast2','feed4')) {
 [FactoryBalance.BatchSearch]::Replay("$p/$profile.catalog.json","$PSScriptRoot/reports/binary-v3/minimum.policy.json","$p/$profile-minimum.json",.5)
 $r=Get-Content "$p/$profile-minimum.json" -Raw|ConvertFrom-Json
 Write-Output "$profile minimum=$([math]::Round($r.seconds/60,2))"
}
