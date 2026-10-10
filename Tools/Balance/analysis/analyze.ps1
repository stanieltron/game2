param([string]$Output = "$PSScriptRoot/../reports")
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../../..")
$driver = Get-Content -Raw "$root/Assets/Editor/FactoryCampaignTest.cs"
function Replace-Required([string]$From, [string]$To) {
    if (-not $script:driver.Contains($From)) { throw "Campaign instrumentation no longer matches: $From" }
    $script:driver = $script:driver.Replace($From, $To)
}
Replace-Required 'public static class FactoryCampaignTest' 'public static class TracedCampaign'
Replace-Required 'public static void Run()' 'public static void Run(string output, bool pauseIdle, bool industrialOnly)'
Replace-Required 'machines.Clear();' 'machines.Clear();FactoryBalance.CampaignTrace.Reset(pauseIdle,industrialOnly);FactoryBalance.CampaignTrace.Begin("Bootstrap and production milestone",sim);'
Replace-Required 'var cost=Reserve(sim.data.techs[t].cost);' 'FactoryBalance.CampaignTrace.Begin(sim.data.techs[t].name,sim);var cost=Reserve(sim.data.techs[t].cost);FactoryBalance.CampaignTrace.Ready(sim);FactoryBalance.CampaignTrace.Activity("research");'
Replace-Required 'Building("industrialMiner");' 'FactoryBalance.CampaignTrace.Begin("Optional industrial equipment and extra drone",sim);Building("industrialMiner");'
Replace-Required 'var delivery=Reserve(Simulation.BayPhases[phase].cost);' 'FactoryBalance.CampaignTrace.Begin("Bay: "+Simulation.BayPhases[phase].name,sim);var delivery=Reserve(Simulation.BayPhases[phase].cost);FactoryBalance.CampaignTrace.Ready(sim);FactoryBalance.CampaignTrace.Activity("bay-phase");'
Replace-Required 'var delivery=Reserve(Simulation.FighterPhases[phase].cost);' 'FactoryBalance.CampaignTrace.Begin("Fighter: "+Simulation.FighterPhases[phase].name,sim);var delivery=Reserve(Simulation.FighterPhases[phase].cost);FactoryBalance.CampaignTrace.Ready(sim);FactoryBalance.CampaignTrace.Activity("fighter-phase");'
$cut = $driver.IndexOf('        var stock=Reserve(Simulation.ReadyStock);')
$end = $driver.IndexOf('    static void Step()')
if ($cut -lt 0 -or $end -lt 0) { throw 'Cannot find campaign endpoint' }
$driver = $driver.Substring(0,$cut) + "        FactoryBalance.CampaignTrace.Save(output,sim);`n    }`n" + $driver.Substring($end)
Replace-Required 'sim.Tick(.5f);' 'FactoryBalance.CampaignTrace.Before(sim);sim.Tick(.5f);FactoryBalance.CampaignTrace.After(sim);'
Replace-Required 'machines[key]=m;while(!sim.Complete(m))Step();return m;' 'machines[key]=m;FactoryBalance.CampaignTrace.Event("build-start",id,sim);FactoryBalance.CampaignTrace.Activity("construction");while(!sim.Complete(m))Step();FactoryBalance.CampaignTrace.Event("build-complete",id,sim);return m;'
Replace-Required 'while(sim.world.cargo.Get(item)<count)Step();' 'FactoryBalance.CampaignTrace.Activity("raw:"+item);while(sim.world.cargo.Get(item)<count)Step();'
Replace-Required 'int before=sim.world.produced.Get(item);while' 'FactoryBalance.CampaignTrace.Activity("production:"+r.id);FactoryBalance.CampaignTrace.Batch(r.id);int before=sim.world.produced.Get(item);while'
Replace-Required 'int before=sim.world.drones;while' 'FactoryBalance.CampaignTrace.Activity("production:drone");FactoryBalance.CampaignTrace.Batch(r.id);int before=sim.world.drones;while'
# Separate source compilation units preserve each file's using directives.
$scratch = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName()+'.cs')
try {
    Set-Content -LiteralPath $scratch -Value $driver
    . "$PSScriptRoot/../load.ps1"
    Import-FactorySimulation @("$PSScriptRoot/Trace.cs", $scratch)
    [IO.Directory]::CreateDirectory($Output) | Out-Null
    Push-Location $root
    try {
        [TracedCampaign]::Run([IO.Path]::GetFullPath("$Output/campaign-baseline.json"),$false,$false)
        [TracedCampaign]::Run([IO.Path]::GetFullPath("$Output/campaign-pause-idle.json"),$true,$false)
        [TracedCampaign]::Run([IO.Path]::GetFullPath("$Output/campaign-retire-basic-generator.json"),$false,$true)
    } finally { Pop-Location }
} finally { Remove-Item -LiteralPath $scratch -ErrorAction SilentlyContinue }
