function Import-FactorySimulation([string[]]$ExtraSources) {
    $balanceRoot = [IO.Path]::GetFullPath("$PSScriptRoot/../..")
    $simulationPaths = @('FactorySimulation','FactoryLogistics','FactoryRecovery','FactoryPowerPriority','FactoryMegaproject') | ForEach-Object { "$balanceRoot/Assets/Scripts/$_.cs" }
    $temporarySources = @()
    try {
        # These game files contain both simulation and UI classes. Keep the actual
        # simulation prefix, dropping only the rendering/input FactoryGame class.
        foreach ($name in @('FactoryBridges','FactoryBeltPlacement')) {
            $sourcePath = "$balanceRoot/Assets/Scripts/$name.cs"
            if (-not (Test-Path -LiteralPath $sourcePath)) { continue }
            $source = Get-Content -Raw -LiteralPath $sourcePath
            $uiStart = $source.IndexOf('    public partial class FactoryGame')
            if ($uiStart -lt 0) { throw "Expected simulation/UI boundary missing in $sourcePath" }
            $source = $source.Substring(0,$uiStart) + "}`n"
            $temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName()+'.cs')
            Set-Content -LiteralPath $temporaryPath -Value $source
            $temporarySources += $temporaryPath
        }
        Add-Type -Path ($simulationPaths + $temporarySources + @("$PSScriptRoot/Runner.cs") + $ExtraSources)
    } finally {
        foreach ($temporaryPath in $temporarySources) { Remove-Item -LiteralPath $temporaryPath -ErrorAction SilentlyContinue }
    }
}
