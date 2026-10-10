param(
    [ValidateSet('run','export','test','campaign')][string]$Command = 'run',
    [string[]]$Scenario,
    [string]$Output = "$PSScriptRoot/reports",
    [string]$Catalog = "$PSScriptRoot/../../Assets/Resources/catalog.json"
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion -lt [version]'7.4') { throw 'Run with PowerShell 7.4 or newer (pwsh).' }
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
. "$PSScriptRoot/load_indexed.ps1"
if (-not ('FactoryBalance.Runner' -as [type])) {
    Import-FactorySimulation @("$PSScriptRoot/Tests.cs", "$root/Assets/Editor/FactoryCampaignTest.cs", "$root/Assets/Editor/FactoryRecoveryTests.cs", "$root/Assets/Editor/FactoryMegaprojectTests.cs")
}
[FactoryBalance.Runner]::SourceRoot = $root
[IO.Directory]::CreateDirectory($Output) | Out-Null
switch ($Command) {
    'export' { [FactoryBalance.Runner]::Export($Catalog, $Output) }
    'test' {
        [FactoryBalance.Tests]::Run($Catalog)
        [FactoryRecoveryTests]::Run([FactoryBalance.Runner]::Load($Catalog))
        [FactoryMegaprojectTests]::Run([FactoryBalance.Runner]::Load($Catalog))
    }
    'campaign' {
        Push-Location $root
        try { [IO.Directory]::CreateDirectory("$root/Logs") | Out-Null; [FactoryCampaignTest]::Run() }
        finally { Pop-Location }
    }
    'run' {
        if (-not $Scenario) { $Scenario = @("$PSScriptRoot/scenarios/industry-first.json", "$PSScriptRoot/scenarios/power-first.json") }
        $results = foreach ($path in $Scenario) { [FactoryBalance.Runner]::Run($Catalog, $path, $Output) }
        $results | Select-Object name,success,seconds,tech,brownoutSeconds,error | Format-Table -AutoSize
        $results | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath "$Output/comparison.json"
        if ($results.Where({ -not $_.success }).Count) { throw 'One or more scenarios failed; inspect the reports.' }
    }
}
