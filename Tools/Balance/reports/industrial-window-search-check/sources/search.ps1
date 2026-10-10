param(
 [int]$Trials=1000,[int]$Seed=73421,[int]$Workers=4,[int]$BatchSize=24,
 [float]$Step=0.5,[string]$Catalog,[string]$Output,[string]$Policy
)
$ErrorActionPreference='Stop'
if ($Trials -lt 1 -or $Workers -lt 1 -or $BatchSize -lt 1 -or $Step -le 0 -or $Step -gt 0.5) {throw 'Use positive trials/workers/batch size and a timestep in (0, 0.5].'}
if (-not $Catalog) {
 if ($Policy) {$Catalog=Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($Policy))) 'catalog.snapshot.json'}
 else {$Catalog="$PSScriptRoot/../../Assets/Resources/catalog.json"}
}
if (-not $Output) {$Output=Join-Path "$PSScriptRoot/reports" ('search-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
. "$PSScriptRoot/load.ps1"
Import-FactorySimulation @("$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/BatchSearch.cs")
if ($Policy) {
 if(Test-Path -LiteralPath $Output){throw 'Replay output already exists; choose a new file.'}
 [FactoryBalance.BatchSearch]::Replay($Catalog,$Policy,$Output,$Step)
} else {
 if ((Test-Path -LiteralPath $Output) -and (Get-ChildItem -LiteralPath $Output -Force | Select-Object -First 1)) {throw 'Search output is not empty; choose a new directory.'}
 [IO.Directory]::CreateDirectory($Output) | Out-Null
 $snapshot=Join-Path $Output 'sources'
 [IO.Directory]::CreateDirectory($snapshot) | Out-Null
 $sourceFiles=@(Get-ChildItem "$PSScriptRoot/../../Assets/Scripts" -Filter 'Factory*.cs')+@(Get-Item "$PSScriptRoot/Runner.cs","$PSScriptRoot/FactoryPlanner.cs","$PSScriptRoot/SearchPolicy.cs","$PSScriptRoot/BatchSearch.cs","$PSScriptRoot/load.ps1","$PSScriptRoot/search.ps1","$PSScriptRoot/search_report.py")
 $hashes=foreach($sourceFile in $sourceFiles){Copy-Item -LiteralPath $sourceFile.FullName -Destination $snapshot; [PSCustomObject]@{name=$sourceFile.Name;sha256=(Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash}}
 ConvertTo-Json -InputObject @($hashes) | Set-Content (Join-Path $Output 'source-hashes.json')
 [FactoryBalance.BatchSearch]::Run($Catalog,$Output,$Trials,$Seed,$Workers,$Step,$BatchSize)
 & python "$PSScriptRoot/search_report.py" $Output
 if ($LASTEXITCODE -ne 0) {throw "Simulations were saved, but report generation failed."}
}
Write-Output "Results: $Output"
