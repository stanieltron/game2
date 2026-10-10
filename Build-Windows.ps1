$ErrorActionPreference = 'Stop'
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$arguments = '-batchmode -nographics -quit -projectPath "{0}" -executeMethod FactoryBuild.BuildWindows -logFile "{0}/Logs/build-cli.log"' -f $PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'Logs') | Out-Null
$buildProcess = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) { throw 'Build failed. Check Logs/build-cli.log. Close the editor for this project before a batch build.' }
Write-Output (Join-Path $PSScriptRoot 'Builds/BootstrapFactory/BootstrapFactory.exe')
